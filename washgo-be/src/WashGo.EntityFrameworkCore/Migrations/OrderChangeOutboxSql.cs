namespace WashGo.EntityFrameworkCore.Migrations;

internal static class OrderChangeOutboxSql
{
    public const string Up = """
        CREATE TABLE washgo.order_change_outbox (
            event_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            order_id uuid NOT NULL,
            revision bigint NOT NULL,
            change_type text NOT NULL,
            occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
            previous_merchant_id uuid NULL,
            available_at timestamptz NOT NULL DEFAULT clock_timestamp(),
            lease_token uuid NULL
        );
        CREATE INDEX ix_order_change_outbox_available ON washgo.order_change_outbox (available_at, event_id);

        CREATE FUNCTION washgo.queue_order_change() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE
            kind text := 'OrderUpdated';
            prior_merchant uuid;
        BEGIN
            IF TG_OP = 'INSERT' THEN
                kind := 'OrderCreated';
            ELSIF NEW.merchant_id IS DISTINCT FROM OLD.merchant_id THEN
                kind := 'MerchantChanged';
                prior_merchant := OLD.merchant_id;
            ELSIF NEW.actuals_confirmed_at IS DISTINCT FROM OLD.actuals_confirmed_at
                OR NEW.actual_weight_kg IS DISTINCT FROM OLD.actual_weight_kg THEN
                kind := 'ActualsChanged';
            ELSIF NEW.payment_status IS DISTINCT FROM OLD.payment_status THEN
                kind := 'PaymentChanged';
            ELSIF NEW.status IS DISTINCT FROM OLD.status THEN
                kind := 'StatusChanged';
            ELSIF NEW.shipper_id IS DISTINCT FROM OLD.shipper_id THEN
                kind := 'AssignmentChanged';
            END IF;
            INSERT INTO washgo.order_change_outbox (order_id, revision, change_type, previous_merchant_id)
                VALUES (NEW.id, NEW.revision, kind, prior_merchant);
            RETURN NEW;
        END;
        $$;
        CREATE TRIGGER trg_order_change_outbox AFTER INSERT OR UPDATE ON washgo.orders
            FOR EACH ROW EXECUTE FUNCTION washgo.queue_order_change();

        CREATE FUNCTION washgo.queue_order_child_change() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF TG_OP = 'UPDATE' THEN
                IF (to_jsonb(NEW) -> TG_ARGV[1]) IS NOT DISTINCT FROM (to_jsonb(OLD) -> TG_ARGV[1])
                    AND (TG_NARGS = 2 OR (to_jsonb(NEW) -> TG_ARGV[2]) IS NOT DISTINCT FROM (to_jsonb(OLD) -> TG_ARGV[2])) THEN
                    RETURN NEW;
                END IF;
            END IF;
            -- A child change signals a reload without locking the parent out of order.
            INSERT INTO washgo.order_change_outbox (order_id, revision, change_type)
                SELECT id, revision, TG_ARGV[0] FROM washgo.orders WHERE id = NEW.order_id;
            RETURN NEW;
        END;
        $$;
        CREATE TRIGGER trg_payment_change_outbox AFTER INSERT OR UPDATE ON washgo.payment_transactions
            FOR EACH ROW EXECUTE FUNCTION washgo.queue_order_child_change('PaymentChanged', 'status', 'session_state');
        CREATE TRIGGER trg_verification_change_outbox AFTER INSERT OR UPDATE ON washgo.payment_verifications
            FOR EACH ROW EXECUTE FUNCTION washgo.queue_order_child_change('PaymentChanged', 'state', 'reason_code');
        CREATE TRIGGER trg_storage_cycle_change_outbox AFTER INSERT OR UPDATE ON washgo.order_storage_cycles
            FOR EACH ROW EXECUTE FUNCTION washgo.queue_order_child_change('StorageChanged', 'state', 'dispatched_at');
        CREATE TRIGGER trg_storage_request_change_outbox AFTER INSERT OR UPDATE ON washgo.storage_return_requests
            FOR EACH ROW EXECUTE FUNCTION washgo.queue_order_child_change('StorageChanged', 'state');
        CREATE TRIGGER trg_delivery_change_outbox AFTER INSERT OR UPDATE ON washgo.delivery_tasks
            FOR EACH ROW EXECUTE FUNCTION washgo.queue_order_child_change('AssignmentChanged', 'status', 'shipper_id');

        CREATE FUNCTION washgo.claim_order_changes(claim_token uuid, batch_size integer)
        RETURNS SETOF washgo.order_change_outbox LANGUAGE sql AS $$
            WITH due AS (
                SELECT item.event_id FROM washgo.order_change_outbox AS item
                WHERE item.available_at <= statement_timestamp()
                ORDER BY item.available_at, item.event_id LIMIT batch_size
                FOR UPDATE SKIP LOCKED
            )
            UPDATE washgo.order_change_outbox AS item
            SET available_at = statement_timestamp() + interval '1 minute', lease_token = claim_token
            FROM due WHERE item.event_id = due.event_id
            RETURNING item.*;
        $$;
        """;

    public const string Down = """
        DROP TRIGGER trg_delivery_change_outbox ON washgo.delivery_tasks;
        DROP TRIGGER trg_storage_request_change_outbox ON washgo.storage_return_requests;
        DROP TRIGGER trg_storage_cycle_change_outbox ON washgo.order_storage_cycles;
        DROP TRIGGER trg_verification_change_outbox ON washgo.payment_verifications;
        DROP TRIGGER trg_payment_change_outbox ON washgo.payment_transactions;
        DROP TRIGGER trg_order_change_outbox ON washgo.orders;
        DROP FUNCTION washgo.claim_order_changes(uuid, integer);
        DROP FUNCTION washgo.queue_order_child_change();
        DROP FUNCTION washgo.queue_order_change();
        DROP TABLE washgo.order_change_outbox;
        """;
}
