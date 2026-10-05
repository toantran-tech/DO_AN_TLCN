-- IMPORTANT: DESIGN REFERENCE ONLY
-- Runtime database source of truth: EF Core migrations in washgo-be/src/WashGo.EntityFrameworkCore/Migrations.
-- Identifier convention: UUID. Application schema: washgo.

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE SCHEMA IF NOT EXISTS washgo;
SET search_path TO washgo, public;
-- ============================================================
-- DATABASE WASHGO - KỲ 1 (PHIÊN BẢN 2)
-- PostgreSQL
-- ------------------------------------------------------------
-- Thay đổi so với wash_go.sql (chỉ gồm nhóm 4, 5, 6, 8):
--
-- [4] Bổ sung bảng/cột cho các chức năng đã có trong tài liệu
--     nhưng chưa có nơi lưu trữ:
--       - service_areas, merchant_service_areas  (vùng phục vụ)
--       - service_change_requests                (đề xuất thêm/sửa dịch vụ)
--       - orders.cancelled_*                     (hủy đơn + phí hủy)
--       - order_status_history                   (lịch sử trạng thái đơn)
--       - otp_codes                              (đăng ký/quên mật khẩu)
--       - users.auth_provider / provider_id      (đăng nhập Google OAuth2)
--       - orders.promotion_id, promotion_usages  (áp mã khuyến mãi)
--       - storage_items                          (đồ chuyển kho lưu trữ)
--       - merchant_settlements, payments.settlement_id (đối soát hoa hồng)
--
-- [5] Bỏ quan hệ hai chiều orders <-> locker_slots:
--       - Xóa cột locker_slots.order_id, chỉ giữ orders.locker_slot_id
--       - Thêm unique index từng phần để 1 ô chỉ phục vụ 1 đơn đang giữ đồ
--
-- [6] Chuẩn hóa khóa ngoại: mọi tham chiếu "khách hàng" trỏ về
--     customers(user_id) thay vì users(id), đồng bộ với merchants(user_id)
--
-- [8] Dọn dẹp nhỏ:
--       - shippers bỏ các cột trùng với users (full_name, phone, email, avatar)
--       - admins bỏ last_login_at (đã có ở users)
--       - Trigger tự động cập nhật updated_at cho toàn bộ bảng
--       - Bổ sung index created_at phục vụ dashboard/báo cáo
--       - Bổ sung config deposit_reminder_minutes (nhắc gửi đồ sau 30 phút)
-- ============================================================

-- ============================================================
-- 1. BẢNG PHÂN QUYỀN & XÁC THỰC
-- ============================================================

CREATE TABLE roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(50) NOT NULL UNIQUE,
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE,  -- TRUE: hoạt động, FALSE: khóa
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO roles (name, description) VALUES
('Customer', 'Khách hàng sử dụng dịch vụ giặt ủi'),
('Merchant', 'Cửa hàng/cơ sở giặt ủi đối tác'),
('Admin', 'Quản trị viên hệ thống'),
('Shipper', 'Nhân viên giao nhận của Merchant');

-- [4] password_hash cho phép NULL + auth_provider/provider_id để hỗ trợ Google OAuth2
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    role_id UUID NOT NULL REFERENCES roles(id),
    email VARCHAR(100) NOT NULL UNIQUE,
    password_hash VARCHAR(255),
    auth_provider VARCHAR(20) NOT NULL DEFAULT 'LOCAL',  -- LOCAL: email/mật khẩu, GOOGLE: Google OAuth2
    provider_id VARCHAR(255),                            -- ID người dùng do nhà cung cấp OAuth cấp
    full_name VARCHAR(100) NOT NULL,
    phone VARCHAR(20),
    avatar JSONB DEFAULT NULL,
    is_active BOOLEAN DEFAULT TRUE,  -- TRUE: hoạt động, FALSE: khóa
    last_login_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    -- Tài khoản LOCAL bắt buộc có mật khẩu; tài khoản OAuth bắt buộc có provider_id
    CONSTRAINT chk_users_auth CHECK (
        (auth_provider = 'LOCAL'  AND password_hash IS NOT NULL) OR
        (auth_provider <> 'LOCAL' AND provider_id   IS NOT NULL)
    ),
    CONSTRAINT unique_users_provider UNIQUE (auth_provider, provider_id)
);

CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_role_id ON users(role_id);

CREATE TABLE refresh_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token VARCHAR(500) NOT NULL UNIQUE,
    expiry_date TIMESTAMP NOT NULL,
    is_revoked BOOLEAN DEFAULT FALSE,  -- FALSE: còn hiệu lực, TRUE: đã thu hồi
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_refresh_tokens_user_id ON refresh_tokens(user_id);
CREATE INDEX idx_refresh_tokens_token ON refresh_tokens(token);

CREATE TABLE otp_codes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,  -- NULL khi đăng ký (user chưa tồn tại)
    email VARCHAR(100) NOT NULL,
    otp_code VARCHAR(10) NOT NULL,           -- Mã OTP 6 chữ số
    purpose VARCHAR(50) NOT NULL,            -- REGISTER: đăng ký, FORGOT_PASSWORD: quên mật khẩu, CHANGE_EMAIL: đổi email
    expiry_date TIMESTAMP NOT NULL,          -- Thời hạn OTP (mặc định 5 phút)
    is_used BOOLEAN DEFAULT FALSE,           -- FALSE: chưa dùng, TRUE: đã dùng
    used_at TIMESTAMP,
    attempt_count INTEGER DEFAULT 0,         -- Số lần nhập sai
    max_attempts INTEGER DEFAULT 5,          -- Số lần nhập sai tối đa
    ip_address VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_otp_codes_email ON otp_codes(email);
CREATE INDEX idx_otp_codes_purpose ON otp_codes(purpose);
CREATE INDEX idx_otp_codes_expiry ON otp_codes(expiry_date);
CREATE INDEX idx_otp_codes_email_purpose ON otp_codes(email, purpose, is_used);

-- [8] Bỏ last_login_at (đã có ở users)
CREATE TABLE admins (
    user_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    permissions JSONB DEFAULT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ============================================================
-- 2. BẢNG KHÁCH HÀNG
-- ============================================================

CREATE TABLE customers (
    user_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    address TEXT,
    district VARCHAR(100),
    city VARCHAR(100),
    loyalty_points INTEGER DEFAULT 0,
    total_orders INTEGER DEFAULT 0,
    total_spent DECIMAL(10,2) DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ============================================================
-- 3. VÙNG PHỤC VỤ & MERCHANT (ĐỐI TÁC)
-- ============================================================

-- [4] Bảng mới: Admin khai báo khu vực/chung cư được phục vụ
-- (chức năng Admin 3.2 #8 "Thiết lập vùng phục vụ" và 3.9 #38 "Cấu hình vùng phục vụ")
CREATE TABLE service_areas (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,          -- VD: Chung cư Sunrise City
    code VARCHAR(50) NOT NULL UNIQUE,    -- VD: SRC-Q7
    description TEXT,
    address TEXT,
    district VARCHAR(100) NOT NULL,
    city VARCHAR(100) NOT NULL,
    latitude DECIMAL(10,8),
    longitude DECIMAL(11,8),
    radius_km DECIMAL(5,2) DEFAULT 2,
    is_active BOOLEAN DEFAULT TRUE,  -- TRUE: đang phục vụ, FALSE: ngưng phục vụ
    created_by UUID REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_service_areas_district ON service_areas(district);
CREATE INDEX idx_service_areas_is_active ON service_areas(is_active);

CREATE TABLE merchants (
    user_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    business_name VARCHAR(200) NOT NULL,
    tax_code VARCHAR(50) UNIQUE,
    business_license JSONB DEFAULT NULL,
    address TEXT NOT NULL,
    district VARCHAR(100) NOT NULL,
    city VARCHAR(100) NOT NULL,
    latitude DECIMAL(10,8) NOT NULL,
    longitude DECIMAL(11,8) NOT NULL,
    phone VARCHAR(20) NOT NULL,
    opening_hours JSONB DEFAULT NULL,
    status VARCHAR(50) DEFAULT 'ACTIVE',  -- ACTIVE: đang hoạt động, INACTIVE: ngưng, SUSPENDED: tạm khóa
    rejection_reason TEXT,
    approved_by UUID REFERENCES users(id),
    approved_at TIMESTAMP,
    rating DECIMAL(2,1) DEFAULT 0,
    total_orders INTEGER DEFAULT 0,
    total_revenue DECIMAL(10,2) DEFAULT 0,
    deposit_amount DECIMAL(10,2) DEFAULT 0,
    deposit_updated_at TIMESTAMP,
    contract_start_date TIMESTAMP,
    contract_end_date TIMESTAMP,
    sla_doc_url TEXT,
    commission_rate DECIMAL(5,2) DEFAULT 20.00,
    service_radius_km INTEGER DEFAULT 2,
    current_workload INTEGER DEFAULT 0,
    max_capacity INTEGER DEFAULT 50,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_merchants_status ON merchants(status);
CREATE INDEX idx_merchants_lat_lng ON merchants(latitude, longitude);
CREATE INDEX idx_merchants_district ON merchants(district);
CREATE INDEX idx_merchants_city ON merchants(city);

-- [4] Bảng mới: gán Merchant phụ trách từng vùng (N-N), dùng cho phân công đơn tự động
CREATE TABLE merchant_service_areas (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id UUID NOT NULL REFERENCES merchants(user_id) ON DELETE CASCADE,
    service_area_id UUID NOT NULL REFERENCES service_areas(id) ON DELETE CASCADE,
    priority INTEGER DEFAULT 1,      -- 1 là ưu tiên cao nhất khi nhiều Merchant cùng phục vụ 1 vùng
    is_active BOOLEAN DEFAULT TRUE,  -- TRUE: đang nhận đơn của vùng, FALSE: tạm ngưng
    assigned_by UUID REFERENCES users(id),
    assigned_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_merchant_area UNIQUE (merchant_id, service_area_id)
);

CREATE INDEX idx_merchant_service_areas_merchant_id ON merchant_service_areas(merchant_id);
CREATE INDEX idx_merchant_service_areas_area_id ON merchant_service_areas(service_area_id);

CREATE TABLE merchant_images (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id UUID NOT NULL REFERENCES merchants(user_id) ON DELETE CASCADE,
    image_type VARCHAR(50) NOT NULL,  -- LOGO, STORE_FRONT, INTERIOR, CERTIFICATE
    public_id VARCHAR(255) NOT NULL,
    secure_url VARCHAR(500) NOT NULL,
    is_primary BOOLEAN DEFAULT FALSE,
    display_order INTEGER DEFAULT 0,
    metadata JSONB DEFAULT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_merchant_images_merchant_id ON merchant_images(merchant_id);

-- ============================================================
-- 4. BẢNG SHIPPER (NHÂN VIÊN GIAO NHẬN)
-- ============================================================

-- [8] Bỏ full_name, phone, email, avatar (lấy từ users); user_id trở thành bắt buộc
CREATE TABLE shippers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    merchant_id UUID NOT NULL REFERENCES merchants(user_id) ON DELETE CASCADE,
    vehicle_type VARCHAR(50) DEFAULT 'MOTORBIKE',  -- MOTORBIKE: xe máy, CAR: ô tô, BICYCLE: xe đạp
    vehicle_plate VARCHAR(20),
    is_active BOOLEAN DEFAULT TRUE,  -- TRUE: hoạt động, FALSE: ngưng
    status VARCHAR(50) DEFAULT 'AVAILABLE',  -- AVAILABLE: rảnh, BUSY: đang làm, OFFLINE: nghỉ
    identity_number VARCHAR(20),
    identity_images JSONB DEFAULT NULL,
    total_deliveries INTEGER DEFAULT 0,
    rating DECIMAL(2,1) DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_shippers_merchant_id ON shippers(merchant_id);
CREATE INDEX idx_shippers_status ON shippers(status);

-- ============================================================
-- 5. BẢNG LOCKER (TỦ THÔNG MINH)
-- ============================================================

-- [4] Thêm service_area_id: locker thuộc vùng nào -> biết Merchant nào phục vụ
CREATE TABLE lockers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id UUID NOT NULL REFERENCES merchants(user_id) ON DELETE CASCADE,
    service_area_id UUID REFERENCES service_areas(id) ON DELETE SET NULL,
    name VARCHAR(100) NOT NULL,
    address TEXT NOT NULL,
    district VARCHAR(100),
    city VARCHAR(100),
    latitude DECIMAL(10,8) NOT NULL,
    longitude DECIMAL(11,8) NOT NULL,
    total_slots INTEGER NOT NULL DEFAULT 10,
    available_slots INTEGER NOT NULL DEFAULT 10,
    locker_type VARCHAR(50) DEFAULT 'NORMAL',  -- NORMAL: thường, REFRIGERATED: lạnh
    description TEXT,
    images JSONB DEFAULT NULL,
    status VARCHAR(50) DEFAULT 'ACTIVE',  -- ACTIVE: đang hoạt động, MAINTENANCE: bảo trì, INACTIVE: ngưng
    rejection_reason TEXT,
    approved_by UUID REFERENCES users(id),
    approved_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_lockers_merchant_id ON lockers(merchant_id);
CREATE INDEX idx_lockers_service_area_id ON lockers(service_area_id);
CREATE INDEX idx_lockers_status ON lockers(status);
CREATE INDEX idx_lockers_lat_lng ON lockers(latitude, longitude);

-- [5] Bỏ cột order_id: quan hệ đơn <-> ô tủ chỉ còn một chiều tại orders.locker_slot_id
CREATE TABLE locker_slots (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    locker_id UUID NOT NULL REFERENCES lockers(id) ON DELETE CASCADE,
    slot_number INTEGER NOT NULL,
    status VARCHAR(50) DEFAULT 'AVAILABLE',  -- AVAILABLE: trống, OCCUPIED: có đồ, MAINTENANCE: hỏng, RESERVED: đã đặt
    size VARCHAR(50) DEFAULT 'STANDARD',  -- SMALL: nhỏ, STANDARD: tiêu chuẩn, LARGE: lớn
    last_used_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_locker_slot UNIQUE (locker_id, slot_number)
);

CREATE INDEX idx_locker_slots_locker_id ON locker_slots(locker_id);
CREATE INDEX idx_locker_slots_status ON locker_slots(status);

-- ============================================================
-- 6. BẢNG DỊCH VỤ
-- ============================================================

CREATE TABLE services (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id UUID NOT NULL REFERENCES merchants(user_id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    base_price DECIMAL(10,2) NOT NULL,
    price_per_kg DECIMAL(10,2),
    price_per_item DECIMAL(10,2),
    estimated_time INTEGER NOT NULL,
    category VARCHAR(50) DEFAULT 'WASHING',  -- WASHING: giặt thường, DRY_CLEANING: giặt hấp, IRONING: ủi, COMBO: combo
    allowed_statuses JSONB DEFAULT NULL,
    images JSONB DEFAULT NULL,
    status VARCHAR(50) DEFAULT 'PENDING',  -- PENDING: chờ duyệt, APPROVED: đã duyệt, REJECTED: từ chối
    rejection_reason TEXT,
    approved_by UUID REFERENCES users(id),
    approved_at TIMESTAMP,
    is_active BOOLEAN DEFAULT TRUE,  -- TRUE: đang dùng, FALSE: ngưng
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_services_merchant_id ON services(merchant_id);
CREATE INDEX idx_services_status ON services(status);

-- [4] Bảng mới: lưu nội dung Merchant ĐỀ XUẤT (thêm mới hoặc chỉnh sửa dịch vụ).
-- Dịch vụ đang chạy giữ nguyên cho tới khi Admin duyệt, khi duyệt mới chép
-- proposed_data đè lên services (chức năng Merchant 2.6 #19, #20 và Admin 3.5 #22, #23)
CREATE TABLE service_change_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id UUID NOT NULL REFERENCES merchants(user_id) ON DELETE CASCADE,
    service_id UUID REFERENCES services(id) ON DELETE CASCADE,  -- NULL khi đề xuất dịch vụ mới
    request_type VARCHAR(50) NOT NULL,  -- CREATE: thêm mới, UPDATE: chỉnh sửa, DELETE: ngưng dịch vụ
    proposed_data JSONB NOT NULL,       -- Giá trị đề xuất: name, description, base_price, price_per_kg...
    current_data JSONB DEFAULT NULL,    -- Ảnh chụp giá trị hiện tại để Admin đối chiếu
    reason TEXT,
    status VARCHAR(50) DEFAULT 'PENDING',  -- PENDING: chờ duyệt, APPROVED: đã duyệt, REJECTED: từ chối
    rejection_reason TEXT,
    reviewed_by UUID REFERENCES users(id),
    reviewed_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_service_change_requests_merchant_id ON service_change_requests(merchant_id);
CREATE INDEX idx_service_change_requests_service_id ON service_change_requests(service_id);
CREATE INDEX idx_service_change_requests_status ON service_change_requests(status);

-- ============================================================
-- 7. BẢNG KHUYẾN MÃI
-- (đặt trước orders vì orders tham chiếu promotions)
-- ============================================================

CREATE TABLE promotions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) NOT NULL UNIQUE,
    discount_type VARCHAR(50) NOT NULL,  -- PERCENT: %, FIXED: cố định
    discount_value DECIMAL(10,2) NOT NULL,
    start_date TIMESTAMP NOT NULL,
    end_date TIMESTAMP NOT NULL,
    min_order_amount DECIMAL(10,2),
    max_discount DECIMAL(10,2),
    usage_limit INTEGER,              -- Tổng lượt dùng toàn hệ thống
    usage_limit_per_user INTEGER,     -- [4] Số lượt tối đa mỗi khách hàng
    used_count INTEGER DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,  -- TRUE: còn hiệu lực, FALSE: hết hiệu lực
    created_by UUID REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_promotions_code ON promotions(code);
CREATE INDEX idx_promotions_is_active ON promotions(is_active);

-- ============================================================
-- 8. BẢNG ĐƠN HÀNG (orders)
-- ============================================================

-- [4] Thêm promotion_id + nhóm cột hủy đơn
-- [6] customer_id trỏ về customers(user_id) thay vì users(id)
CREATE TABLE orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(user_id) ON DELETE CASCADE,
    merchant_id UUID REFERENCES merchants(user_id) ON DELETE SET NULL,
    locker_id UUID REFERENCES lockers(id),
    locker_slot_id UUID REFERENCES locker_slots(id),  -- Ô tủ đang giữ đồ của đơn ở thời điểm hiện tại
    promotion_id UUID REFERENCES promotions(id),
    order_code VARCHAR(50) NOT NULL UNIQUE,  -- ORD-YYYYMMDD-XXX
    merchant_order_code VARCHAR(50) UNIQUE,  -- GS-YYYYMMDD-XXX
    total_amount DECIMAL(10,2) DEFAULT 0,
    discount_amount DECIMAL(10,2) DEFAULT 0,
    final_amount DECIMAL(10,2) DEFAULT 0,
    actual_weight_kg DECIMAL(10,2),  -- Số kg thực tế (Merchant cân)
    actual_item_count INTEGER,       -- Số cái thực tế (Merchant đếm)
    status VARCHAR(50) DEFAULT 'PENDING',
    -- PENDING: chờ gửi đồ (QR đã sinh)
    -- DEPOSITED: khách đã gửi đồ vào locker
    -- COLLECTED: shipper đã lấy đồ về tiệm
    -- WASHING: đang giặt
    -- DRYING: đang sấy
    -- IRONING: đang ủi
    -- FOLDING: đang gấp
    -- AWAITING_PAYMENT: đã chốt khối lượng và giá cuối, chờ khách thanh toán
    -- PAID: khách đã thanh toán
    -- READY_TO_RETURN: đủ điều kiện trả đồ về locker
    -- RETURNED: đã trả đồ vào locker, chờ khách nhận
    -- COMPLETED: khách đã nhận đồ
    -- CANCELLED: đã hủy
    -- OVERDUE_RETURNED: trả trễ
    -- OVERDUE_PICKUP: lấy trễ
    -- STORAGE: đang lưu kho
    note TEXT,
    qr_code VARCHAR(500),
    estimated_finish_at TIMESTAMP,
    eta_updated_at TIMESTAMP,
    finished_at TIMESTAMP,
    locker_name VARCHAR(100),
    slot_number INTEGER,
    confirmed_by UUID REFERENCES users(id),
    confirmed_at TIMESTAMP,
    deposit_deadline TIMESTAMP,
    return_deadline TIMESTAMP,
    pickup_deadline TIMESTAMP,
    payment_deadline TIMESTAMP,
    overdue_fee_total DECIMAL(10,2) DEFAULT 0,
    storage_fee_total DECIMAL(10,2) DEFAULT 0,
    -- [4] Nhóm cột hủy đơn (chức năng Customer 1.2 #13 + bảng phí hủy trong tài liệu)
    cancelled_at TIMESTAMP,
    cancelled_by UUID REFERENCES users(id),
    cancelled_by_type VARCHAR(50),  -- CUSTOMER, MERCHANT, ADMIN, SYSTEM
    cancellation_reason TEXT,
    cancel_fee_amount DECIMAL(10,2) DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_orders_customer_id ON orders(customer_id);
CREATE INDEX idx_orders_merchant_id ON orders(merchant_id);
CREATE INDEX idx_orders_order_code ON orders(order_code);
CREATE INDEX idx_orders_merchant_order_code ON orders(merchant_order_code);
CREATE INDEX idx_orders_status ON orders(status);
CREATE INDEX idx_orders_promotion_id ON orders(promotion_id);
CREATE INDEX idx_orders_created_at ON orders(created_at);  -- [8] phục vụ dashboard/báo cáo

-- [5] Thay cho cột locker_slots.order_id đã bỏ:
-- một ô tủ chỉ được gắn với tối đa 1 đơn đang chiếm giữ ô đó
CREATE UNIQUE INDEX idx_orders_active_slot ON orders(locker_slot_id)
WHERE locker_slot_id IS NOT NULL
  AND status IN ('PENDING', 'DEPOSITED', 'RETURNED', 'OVERDUE_PICKUP');

-- [4] Bảng mới: lịch sử chuyển trạng thái đơn (dữ liệu cho AI dự đoán ETA
-- và AI phát hiện bất thường, đồng thời phục vụ tra cứu tranh chấp)
CREATE TABLE order_status_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    from_status VARCHAR(50),           -- NULL khi đơn vừa được tạo
    to_status VARCHAR(50) NOT NULL,
    changed_by UUID REFERENCES users(id),
    changed_by_type VARCHAR(50) NOT NULL,  -- CUSTOMER, MERCHANT, SHIPPER, ADMIN, SYSTEM
    note TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_order_status_history_order_id ON order_status_history(order_id);
CREATE INDEX idx_order_status_history_created_at ON order_status_history(created_at);

-- [4] Bảng mới: lượt sử dụng mã khuyến mãi (chặn 1 khách dùng vượt hạn mức)
CREATE TABLE promotion_usages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    promotion_id UUID NOT NULL REFERENCES promotions(id) ON DELETE CASCADE,
    customer_id UUID NOT NULL REFERENCES customers(user_id) ON DELETE CASCADE,
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    discount_amount DECIMAL(10,2) NOT NULL,
    used_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_promotion_per_order UNIQUE (order_id)
);

CREATE INDEX idx_promotion_usages_promotion_id ON promotion_usages(promotion_id);
CREATE INDEX idx_promotion_usages_customer_id ON promotion_usages(customer_id);

-- [4] Bảng mới: đồ quá 72 giờ không lấy -> chuyển kho lưu trữ (status STORAGE)
CREATE TABLE storage_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    merchant_id UUID NOT NULL REFERENCES merchants(user_id) ON DELETE CASCADE,
    from_locker_id UUID REFERENCES lockers(id),
    from_slot_id UUID REFERENCES locker_slots(id),
    storage_location VARCHAR(200) NOT NULL,  -- Vị trí kho lưu trữ
    moved_by UUID REFERENCES users(id),
    moved_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    storage_days INTEGER DEFAULT 0,
    storage_fee DECIMAL(10,2) DEFAULT 0,
    status VARCHAR(50) DEFAULT 'IN_STORAGE',  -- IN_STORAGE: đang lưu, RETURNED_TO_CUSTOMER: đã trả khách, DISPOSED: đã thanh lý
    released_to INTEGER REFERENCES users(id),
    released_at TIMESTAMP,
    note TEXT,
    images JSONB DEFAULT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_storage_per_order UNIQUE (order_id)
);

CREATE INDEX idx_storage_items_merchant_id ON storage_items(merchant_id);
CREATE INDEX idx_storage_items_status ON storage_items(status);

-- ============================================================
-- 9. BẢNG DELIVERY ASSIGNMENTS
-- ============================================================

CREATE TABLE delivery_assignments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    shipper_id UUID NOT NULL REFERENCES shippers(id) ON DELETE CASCADE,
    task_type VARCHAR(50) NOT NULL,  -- PICKUP: lấy đồ, DELIVERY: trả đồ
    status VARCHAR(50) DEFAULT 'ASSIGNED',  -- ASSIGNED: đã phân công, IN_PROGRESS: đang thực hiện, COMPLETED: hoàn thành, CANCELLED: đã hủy
    assigned_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    cancelled_at TIMESTAMP,
    cancellation_reason TEXT,
    notes TEXT,
    confirmation_images JSONB DEFAULT NULL,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_delivery_assignments_order_id ON delivery_assignments(order_id);
CREATE INDEX idx_delivery_assignments_shipper_id ON delivery_assignments(shipper_id);
CREATE INDEX idx_delivery_assignments_status ON delivery_assignments(status);

-- ============================================================
-- 10. BẢNG ORDER ITEMS
-- ============================================================

CREATE TABLE order_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    service_id UUID NOT NULL REFERENCES services(id),
    quantity INTEGER DEFAULT 0,  -- Số cái (cho mền, chăn, gối)
    weight_kg DECIMAL(10,2),  -- Số kg (cho quần áo)
    unit_price DECIMAL(10,2) NOT NULL,
    total_price DECIMAL(10,2) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_order_items_order_id ON order_items(order_id);
CREATE INDEX idx_order_items_service_id ON order_items(service_id);

-- ============================================================
-- 11. BẢNG THANH TOÁN, ĐỐI SOÁT & HOÀN TIỀN
-- ============================================================

-- [4] Bảng mới: kỳ đối soát/chi trả cho Merchant sau khi trừ hoa hồng
-- (chức năng Admin 3.8 #32, #33)
CREATE TABLE merchant_settlements (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id UUID NOT NULL REFERENCES merchants(user_id) ON DELETE CASCADE,
    settlement_code VARCHAR(50) NOT NULL UNIQUE,  -- STL-YYYYMM-XXX
    period_start DATE NOT NULL,
    period_end DATE NOT NULL,
    total_orders INTEGER DEFAULT 0,
    gross_amount DECIMAL(10,2) DEFAULT 0,       -- Tổng tiền khách đã thanh toán
    commission_amount DECIMAL(10,2) DEFAULT 0,  -- Hoa hồng nền tảng giữ lại
    adjustment_amount DECIMAL(10,2) DEFAULT 0,  -- Bồi thường/phí phạt cấn trừ
    net_amount DECIMAL(10,2) DEFAULT 0,         -- Số thực trả Merchant
    status VARCHAR(50) DEFAULT 'PENDING',  -- PENDING: chờ chi, PAID: đã chi, CANCELLED: đã hủy
    paid_at TIMESTAMP,
    paid_by UUID REFERENCES users(id),
    transaction_code VARCHAR(100),
    note TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_merchant_period UNIQUE (merchant_id, period_start, period_end)
);

CREATE INDEX idx_merchant_settlements_merchant_id ON merchant_settlements(merchant_id);
CREATE INDEX idx_merchant_settlements_status ON merchant_settlements(status);

CREATE TABLE payments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    settlement_id UUID REFERENCES merchant_settlements(id) ON DELETE SET NULL,  -- [4] thuộc kỳ đối soát nào
    amount DECIMAL(10,2) NOT NULL,
    platform_fee DECIMAL(10,2) DEFAULT 0,
    merchant_amount DECIMAL(10,2) DEFAULT 0,
    payment_method VARCHAR(50) NOT NULL,  -- VNPAY, MOMO
    transaction_code VARCHAR(100),
    status VARCHAR(50) DEFAULT 'PENDING',  -- PENDING: chờ, SUCCESS: thành công, FAILED: thất bại
    payment_date TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_payments_order_id ON payments(order_id);
CREATE INDEX idx_payments_status ON payments(status);
CREATE INDEX idx_payments_settlement_id ON payments(settlement_id);
CREATE INDEX idx_payments_created_at ON payments(created_at);  -- [8] phục vụ báo cáo doanh thu

CREATE TABLE refunds (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    payment_id UUID REFERENCES payments(id),
    amount DECIMAL(10,2) NOT NULL,
    fee DECIMAL(10,2) DEFAULT 0,
    method VARCHAR(50) NOT NULL,  -- AUTO: tự động, MANUAL: thủ công
    status VARCHAR(50) DEFAULT 'PENDING',  -- PENDING: chờ, SUCCESS: thành công, FAILED: thất bại
    transaction_code VARCHAR(100),
    refund_date TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_refunds_order_id ON refunds(order_id);
CREATE INDEX idx_refunds_status ON refunds(status);

CREATE TABLE refund_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    refund_amount DECIMAL(10,2) NOT NULL,
    fee_amount DECIMAL(10,2) DEFAULT 0,
    reason TEXT,
    status VARCHAR(50) DEFAULT 'PENDING',  -- PENDING: chờ duyệt, APPROVED: đã duyệt, REJECTED: từ chối
    approved_by UUID REFERENCES users(id),
    approved_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_refund_requests_order_id ON refund_requests(order_id);
CREATE INDEX idx_refund_requests_status ON refund_requests(status);

CREATE TABLE overdue_fees (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    fee_type VARCHAR(50) NOT NULL,  -- DEPOSIT_TIMEOUT: quá hạn gửi, PICKUP_TIMEOUT: quá hạn lấy, RETURN_TIMEOUT: quá hạn trả, STORAGE_FEE: phí lưu kho
    amount DECIMAL(10,2) NOT NULL,
    days_overdue INTEGER DEFAULT 0,
    status VARCHAR(20) DEFAULT 'PENDING',  -- PENDING: chờ, PAID: đã thu, WAIVED: bỏ qua
    paid_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_overdue_fees_order_id ON overdue_fees(order_id);
CREATE INDEX idx_overdue_fees_status ON overdue_fees(status);

-- ============================================================
-- 12. BẢNG ĐÁNH GIÁ & KHIẾU NẠI
-- ============================================================

-- [6] customer_id trỏ về customers(user_id)
CREATE TABLE ratings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    customer_id UUID NOT NULL REFERENCES customers(user_id) ON DELETE CASCADE,
    rating INTEGER NOT NULL CHECK (rating >= 1 AND rating <= 5),
    comment TEXT,
    images JSONB DEFAULT NULL,
    criteria JSONB DEFAULT NULL,
    status VARCHAR(20) DEFAULT 'PUBLISHED',  -- PUBLISHED: hiển thị, HIDDEN: ẩn
    is_verified BOOLEAN DEFAULT TRUE,
    verified_by UUID REFERENCES users(id),
    verified_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_rating_per_order UNIQUE (order_id)
);

CREATE INDEX idx_ratings_customer_id ON ratings(customer_id);
CREATE INDEX idx_ratings_rating ON ratings(rating);

-- [6] customer_id trỏ về customers(user_id)
CREATE TABLE damage_reports (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    customer_id UUID NOT NULL REFERENCES customers(user_id) ON DELETE CASCADE,
    merchant_id UUID NOT NULL REFERENCES merchants(user_id) ON DELETE CASCADE,
    shipper_id UUID REFERENCES shippers(id),
    description TEXT NOT NULL,
    severity VARCHAR(50) DEFAULT 'MEDIUM',  -- LIGHT: nhẹ, MEDIUM: trung bình, SEVERE: nặng
    images JSONB DEFAULT NULL,
    status VARCHAR(50) DEFAULT 'PENDING',  -- PENDING: chờ, INVESTIGATING: đang điều tra, RESOLVED: đã giải quyết, REJECTED: từ chối
    responsible_party VARCHAR(50),  -- SHIPPER, MERCHANT, CUSTOMER, UNKNOWN
    resolution TEXT,
    refund_amount DECIMAL(10,2),
    compensation_amount DECIMAL(10,2) DEFAULT 0,
    resolved_by UUID REFERENCES users(id),
    resolved_at TIMESTAMP,
    photo_deposit_url VARCHAR(500),
    photo_collect_url VARCHAR(500),
    photo_received_url VARCHAR(500),
    photo_return_url VARCHAR(500),
    photo_withdraw_url VARCHAR(500),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_damage_reports_order_id ON damage_reports(order_id);
CREATE INDEX idx_damage_reports_status ON damage_reports(status);

-- ============================================================
-- 13. BẢNG THÔNG BÁO & LOG
-- ============================================================

CREATE TABLE notifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    user_type VARCHAR(50) NOT NULL,  -- CUSTOMER, MERCHANT, ADMIN, SHIPPER
    title VARCHAR(200) NOT NULL,
    message TEXT NOT NULL,
    type VARCHAR(50) NOT NULL,
    data JSONB DEFAULT NULL,
    link VARCHAR(500),
    priority VARCHAR(20) DEFAULT 'NORMAL',  -- HIGH, NORMAL, LOW
    is_read BOOLEAN DEFAULT FALSE,  -- FALSE: chưa đọc, TRUE: đã đọc
    is_deleted BOOLEAN DEFAULT FALSE,  -- FALSE: còn, TRUE: đã xóa
    read_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_notifications_user_id ON notifications(user_id);
CREATE INDEX idx_notifications_user_type ON notifications(user_type);
CREATE INDEX idx_notifications_is_read ON notifications(is_read);
CREATE INDEX idx_notifications_user_unread ON notifications(user_id, is_read);  -- [8] đếm thông báo chưa đọc
CREATE INDEX idx_notifications_created_at ON notifications(created_at);         -- [8] sắp xếp theo thời gian

CREATE TABLE notification_templates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    type VARCHAR(50) NOT NULL UNIQUE,
    title_template VARCHAR(200) NOT NULL,
    message_template TEXT NOT NULL,
    user_type VARCHAR(50) NOT NULL,  -- CUSTOMER, MERCHANT, ADMIN, SHIPPER
    priority VARCHAR(20) DEFAULT 'NORMAL',
    is_active BOOLEAN DEFAULT TRUE,  -- TRUE: đang dùng, FALSE: không dùng
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO notification_templates (type, title_template, message_template, user_type, priority) VALUES
('ORDER_CREATED', 'Đơn hàng đã tạo', 'Đơn hàng #{{orderCode}} đã được tạo. Vui lòng đến locker {{lockerName}}, ô số {{slotNumber}} để gửi đồ.', 'CUSTOMER', 'HIGH'),
('ORDER_DEPOSITED', 'Đã gửi đồ thành công', 'Bạn đã gửi đồ thành công vào Locker {{lockerName}}, ô số {{slotNumber}}.', 'CUSTOMER', 'HIGH'),
('NEW_ORDER', 'Đơn hàng mới cần gán', 'Có đơn hàng mới #{{orderCode}} cần gán Merchant.', 'ADMIN', 'HIGH'),
('ORDER_ASSIGNED', 'Đơn hàng mới được gán', 'Bạn có đơn hàng mới #{{orderCode}} cần xử lý.', 'MERCHANT', 'HIGH'),
('ORDER_READY', 'Đồ đang được trả về', 'Đơn #{{orderCode}} đã thanh toán và đang được trả về Locker {{lockerName}}.', 'CUSTOMER', 'HIGH'),
('PICKUP_REMINDER_1', 'Nhắc nhở lấy đồ lần 1', 'Đồ của bạn đang chờ tại Locker {{lockerName}}, ô số {{slotNumber}}. Còn 24 giờ để lấy miễn phí.', 'CUSTOMER', 'NORMAL'),
('PICKUP_REMINDER_2', 'Nhắc nhở lấy đồ lần 2', 'Đồ của bạn đã quá 48 giờ chưa lấy. Phí phạt 10.000đ/ngày sẽ được áp dụng.', 'CUSTOMER', 'HIGH'),
('OVERDUE_FEE', 'Thông báo phí phạt', 'Phí phạt lưu locker quá hạn: {{feeAmount}}đ đã được thêm vào đơn hàng #{{orderCode}}.', 'CUSTOMER', 'HIGH'),
('COMPLAINT_NEW', 'Khiếu nại mới', 'Có khiếu nại mới từ khách {{customerName}} về đơn #{{orderCode}}.', 'ADMIN', 'HIGH'),
('AI_ANOMALY', 'Cảnh báo bất thường', 'Cảnh báo: Đơn #{{orderCode}} đã quá hạn {{overdueHours}} giờ!', 'ADMIN', 'HIGH'),
('PAYMENT_SUCCESS', 'Thanh toán thành công', 'Đơn #{{orderCode}} đã thanh toán. WashGo sẽ trả đồ về locker để bạn nhận.', 'CUSTOMER', 'HIGH'),
('PAYMENT_REMINDER', 'Nhắc nhở thanh toán', 'Đơn #{{orderCode}} đã chốt giá {{amount}}đ. Vui lòng thanh toán để WashGo trả đồ về locker.', 'CUSTOMER', 'HIGH'),
('DELIVERY_ASSIGNED', 'Đơn mới được phân công', 'Bạn có đơn mới #{{orderCode}} cần lấy đồ tại Locker {{lockerName}}.', 'SHIPPER', 'HIGH'),
('DELIVERY_REMINDER', 'Nhắc nhở lấy đồ', 'Đơn #{{orderCode}} đã chờ 30 phút. Vui lòng đến lấy.', 'SHIPPER', 'NORMAL'),
('DELIVERY_RETURN_REMINDER', 'Nhắc nhở trả đồ', 'Đơn #{{orderCode}} đã giặt xong. Vui lòng trả vào locker.', 'SHIPPER', 'NORMAL'),
-- [4] Template cho các chức năng mới bổ sung
('ORDER_CANCELLED', 'Đơn hàng bị hủy', 'Đơn hàng #{{orderCode}} đã bị hủy. Lý do: {{reason}}.', 'CUSTOMER', 'HIGH'),
('DEPOSIT_REMINDER', 'Nhắc nhở gửi đồ', 'Bạn còn {{minutesLeft}} phút để gửi đồ vào Locker {{lockerName}}, ô số {{slotNumber}}. Quá hạn đơn hàng sẽ bị hủy.', 'CUSTOMER', 'HIGH'),
('STORAGE_MOVED', 'Thông báo chuyển kho', 'Đồ của bạn đã được chuyển về kho lưu trữ. Vui lòng liên hệ để nhận.', 'CUSTOMER', 'HIGH'),
('SERVICE_REQUEST_NEW', 'Đề xuất dịch vụ mới', 'Merchant {{merchantName}} đề xuất dịch vụ {{serviceName}}, cần phê duyệt.', 'ADMIN', 'HIGH'),
('SERVICE_REQUEST_REVIEWED', 'Kết quả phê duyệt dịch vụ', 'Đề xuất {{requestType}} của bạn đã được {{result}}.', 'MERCHANT', 'HIGH'),
('SETTLEMENT_PAID', 'Đã chi trả đối soát', 'Kỳ đối soát {{settlementCode}} đã được chi trả: {{netAmount}}đ.', 'MERCHANT', 'NORMAL');

CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    user_type VARCHAR(50) NOT NULL,  -- CUSTOMER, MERCHANT, ADMIN, SHIPPER, SYSTEM
    action VARCHAR(100) NOT NULL,  -- LOGIN, LOGOUT, CREATE, UPDATE, DELETE, APPROVE, REJECT
    entity_type VARCHAR(100) NOT NULL,
    entity_id UUID,
    old_value JSONB DEFAULT NULL,
    new_value JSONB DEFAULT NULL,
    ip_address VARCHAR(50),
    user_agent TEXT,
    device_type VARCHAR(50),
    status VARCHAR(20) DEFAULT 'SUCCESS',  -- SUCCESS: thành công, FAILED: thất bại
    error_message TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_audit_logs_user_id ON audit_logs(user_id);
CREATE INDEX idx_audit_logs_action ON audit_logs(action);
CREATE INDEX idx_audit_logs_entity_type ON audit_logs(entity_type);
CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at);

CREATE TABLE qr_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id),
    user_type VARCHAR(50) NOT NULL,  -- CUSTOMER, MERCHANT, SHIPPER, ADMIN
    action VARCHAR(50) NOT NULL,  -- DEPOSIT: gửi đồ, COLLECT: lấy đồ về, RETURN: trả đồ, WITHDRAW: lấy đồ
    locker_id UUID NOT NULL REFERENCES lockers(id),
    slot_id UUID NOT NULL REFERENCES locker_slots(id),
    is_success BOOLEAN DEFAULT TRUE,  -- TRUE: thành công, FALSE: thất bại
    ip_address VARCHAR(50),
    user_agent TEXT,
    error_message TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_qr_logs_order_id ON qr_logs(order_id);
CREATE INDEX idx_qr_logs_user_id ON qr_logs(user_id);

-- ============================================================
-- 14. BẢNG CẤU HÌNH HỆ THỐNG
-- ============================================================

CREATE TABLE chat_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    session_id VARCHAR(100) NOT NULL UNIQUE,
    messages JSONB DEFAULT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_chat_sessions_user_id ON chat_sessions(user_id);

CREATE TABLE system_configs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    config_key VARCHAR(100) NOT NULL UNIQUE,
    config_value TEXT NOT NULL,
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE,  -- TRUE: đang dùng, FALSE: không dùng
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO system_configs (config_key, config_value, description) VALUES
('otp_length', '6', 'Độ dài mã OTP'),
('otp_expiry_minutes', '5', 'Thời gian hết hạn OTP (phút)'),
('otp_max_attempts', '5', 'Số lần nhập sai OTP tối đa'),
('otp_resend_cooldown_seconds', '60', 'Thời gian chờ giữa 2 lần gửi OTP (giây)'),
('default_price', '50000', 'Giá mặc định cho dịch vụ giặt'),
('commission_rate', '30', 'Phí hoa hồng nền tảng (%)'),
('default_sla_hours', '24', 'Thời gian xử lý tối đa (giờ)'),
('max_refund_percentage', '100', 'Tỷ lệ hoàn tiền tối đa (%)'),
('max_damage_report_days', '3', 'Số ngày tối đa để báo cáo hư hỏng'),
('deposit_timeout_minutes', '30', 'Thời gian tối đa để khách gửi đồ sau khi tạo đơn (phút)'),
('deposit_reminder_minutes', '30', 'Thời điểm nhắc nhở khách gửi đồ sau khi tạo đơn (phút)'),
('merchant_collect_timeout_hours', '4', 'Thời gian tối đa để Merchant lấy đồ (giờ)'),
('return_timeout_hours', '24', 'Thời gian tối đa để Merchant trả đồ (giờ)'),
('pickup_grace_hours', '48', 'Thời gian miễn phí để khách lấy đồ (giờ)'),
('payment_timeout_hours', '24', 'Thời gian tối đa để khách thanh toán sau khi lấy đồ (giờ)'),
('overdue_fee_0_24', '10000', 'Phí phạt 0-24h (đồng/ngày)'),
('overdue_fee_24_48', '20000', 'Phí phạt 24-48h (đồng/ngày)'),
('overdue_fee_48_72', '50000', 'Phí phạt 48-72h (đồng/ngày)'),
('storage_fee_per_day', '100000', 'Phí lưu kho (đồng/ngày)'),
('storage_days', '7', 'Số ngày tối đa lưu kho trước khi xử lý'),
('cancel_penalty_within_30', '0', 'Phí hủy trong 30 phút (%)'),
('cancel_penalty_after_30', '30', 'Phí hủy sau 30 phút - 24h (%)'),
('cancel_penalty_after_24h', '100', 'Phí hủy sau 24h (%)'),
('compensation_light', '30', 'Bồi thường hư nhẹ (%)'),
('compensation_medium', '70', 'Bồi thường hư trung bình (%)'),
('compensation_severe', '100', 'Bồi thường hư nặng (%)'),
('compensation_lost', '200', 'Bồi thường mất đồ (%)'),
('eta_buffer_minutes', '15', 'Thời gian đệm an toàn (phút)'),
('eta_weight_factor', '0.3', 'Hệ số ảnh hưởng của khối lượng'),
('eta_workload_factor', '0.5', 'Hệ số ảnh hưởng của tải lượng');

-- ============================================================
-- 15. [8] TRIGGER TỰ ĐỘNG CẬP NHẬT updated_at
-- ============================================================

CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Gắn trigger cho mọi bảng có cột updated_at
DO $$
DECLARE
    tbl TEXT;
BEGIN
    FOR tbl IN
        SELECT c.table_name
        FROM information_schema.columns c
        JOIN information_schema.tables t
          ON t.table_schema = c.table_schema AND t.table_name = c.table_name
        WHERE c.table_schema = 'public'
          AND c.column_name = 'updated_at'
          AND t.table_type = 'BASE TABLE'
    LOOP
        EXECUTE format(
            'CREATE TRIGGER trg_%1$s_updated_at BEFORE UPDATE ON %1$I
             FOR EACH ROW EXECUTE FUNCTION set_updated_at();', tbl);
    END LOOP;
END;
$$;
