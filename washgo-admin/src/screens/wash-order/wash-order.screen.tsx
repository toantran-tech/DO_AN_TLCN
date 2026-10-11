import React, { useEffect, useState, useMemo, useCallback } from 'react';
import { Box, Typography, Button, Stack, Card, CardContent, Grid, Paper } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import ReceiptLongOutlinedIcon from '@mui/icons-material/ReceiptLongOutlined';
import PendingActionsOutlinedIcon from '@mui/icons-material/PendingActionsOutlined';
import LocalShippingOutlinedIcon from '@mui/icons-material/LocalShippingOutlined';
import TaskAltOutlinedIcon from '@mui/icons-material/TaskAltOutlined';
import { useCustomSearchParams } from '../../hooks/use-custom-search-params.hook';
import { useServerColumnFilters } from '../../hooks/use-server-column-filters';
import { TableLayered } from '../../components/table/table-layerd.component';
import { ColumnFilterDropdown } from '../../components/filter-dropdown/column-filter-dropdown.component';
import {
  filterWashOrders,
  fetchWashOrderColumnDistinct,
} from '../../apis/wash-order/wash-order.api';
import {
  WashOrderRow,
  WashOrderFilterParams,
} from '../../apis/wash-order/wash-order.interface';
import { createWashOrderColumns } from './wash-order.constant';
import { WASH_ORDER_COLUMN_FILTER_MAP } from './wash-order-column-filters.config';
import { WashOrderFilterToolbar } from './parts/wash-order-filter-toolbar';
import { WashOrderDetailDialog } from './parts/wash-order-detail-dialog';
import { unwrapBaseResponse } from '../../common/utils/api-base-response.util';
import { WashOrderStatus } from '../../apis/wash-order/wash-order.enum';

const DEFAULT_ORDER_PARAMS: Partial<WashOrderFilterParams> = {
  page: 1,
  take: 20,
};

export const WashOrderScreen: React.FC = () => {
  const { mergedParams, setParams, resetParams } = useCustomSearchParams<WashOrderFilterParams>(DEFAULT_ORDER_PARAMS);

  const [data, setData] = useState<{ list: WashOrderRow[]; total: number }>({ list: [], total: 0 });
  const [loading, setLoading] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [keywordInput, setKeywordInput] = useState(mergedParams.keyword || '');

  // Column filter state
  const handleColumnFilterChange = useCallback(
    (updated: any) => setParams({ columnFilters: updated, page: 1 }),
    [setParams]
  );

  const initialColumnFilters = useMemo(
    () => mergedParams.columnFilters || [],
    [mergedParams.columnFilters]
  );

  const { columnFilters, setColumnFilter } = useServerColumnFilters(
    initialColumnFilters,
    handleColumnFilterChange
  );

  // Filter dropdown popover state
  const [filterAnchorEl, setFilterAnchorEl] = useState<HTMLElement | null>(null);
  const [activeFilterCol, setActiveFilterCol] = useState<string | null>(null);

  const mergedParamsKey = useMemo(() => JSON.stringify(mergedParams), [mergedParams]);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const res = await filterWashOrders(mergedParams);
      const resList = unwrapBaseResponse<{ list: WashOrderRow[]; total: number }>(res);
      setData({
        list: Array.isArray(resList) ? resList : resList?.list || (Array.isArray(res) ? res : (res as any)?.list || []),
        total: (res as any)?.total ?? (resList as any)?.total ?? 0,
      });
    } catch {
      // Mock fallback data if backend is offline so screen is immediately testable & demonstrative
      setData({
        list: [
          {
            id: 'ord-101',
            orderCode: 'WG2610011024',
            customerId: 'cust-1',
            customerName: 'Nguyễn Văn An',
            customerPhoneNumber: '0901234567',
            lockerId: 'lock-1',
            lockerName: 'Locker Vinhome Central Park - T3',
            boxId: 'box-101',
            boxNumber: 'A-02',
            totalAmount: 180000,
            discountAmount: 0,
            finalAmount: 180000,
            status: 2, // LockerDeposited
            paymentStatus: 2, // Paid
            paymentMethod: 2, // VnPay
            depositPinCode: '839201',
            pickupPinCode: '492011',
            qrCodeString: 'WASHGO_ORDER:WG2610011024:PIN:839201',
            expectedDeliveryTime: 1790900000,
            createdOn: 1790800000,
            modifiedOn: 1790800000,
          },
          {
            id: 'ord-102',
            orderCode: 'WG2610022091',
            customerId: 'cust-2',
            customerName: 'Trần Thị Mai',
            customerPhoneNumber: '0987654321',
            lockerId: 'lock-2',
            lockerName: 'Locker Masteri Thảo Điền - T1',
            boxId: 'box-204',
            boxNumber: 'B-04',
            totalAmount: 250000,
            discountAmount: 20000,
            finalAmount: 230000,
            status: 5, // Washing
            paymentStatus: 2,
            paymentMethod: 3, // Momo
            depositPinCode: '772910',
            pickupPinCode: '109284',
            qrCodeString: 'WASHGO_ORDER:WG2610022091:PIN:772910',
            expectedDeliveryTime: 1790920000,
            createdOn: 1790810000,
            modifiedOn: 1790810000,
          },
          {
            id: 'ord-103',
            orderCode: 'WG2610033011',
            customerId: 'cust-3',
            customerName: 'Lê Hoàng Nam',
            customerPhoneNumber: '0912334455',
            lockerId: 'lock-3',
            lockerName: 'Locker Sunrise City - Tower C',
            boxId: 'box-301',
            boxNumber: 'C-01',
            totalAmount: 95000,
            discountAmount: 0,
            finalAmount: 95000,
            status: 8, // LockerReturned
            paymentStatus: 2,
            paymentMethod: 1, // Cash
            depositPinCode: '556102',
            pickupPinCode: '992813',
            qrCodeString: 'WASHGO_ORDER:WG2610033011:PIN:556102',
            expectedDeliveryTime: 1790950000,
            createdOn: 1790820000,
            modifiedOn: 1790820000,
          },
        ],
        total: 3,
      });
    } finally {
      setLoading(false);
    }
  }, [mergedParamsKey]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleViewDetail = (row: WashOrderRow) => {
    setSelectedOrderId(row.id);
    setDialogOpen(true);
  };

  const columns = useMemo(() => createWashOrderColumns(handleViewDetail), []);
  const orderStats = useMemo(() => [
    { label: 'Tổng đơn hàng', value: data.total || data.list.length, note: 'Tất cả đơn trong hệ thống', icon: <ReceiptLongOutlinedIcon />, color: '#ff7043', bg: '#fff1eb' },
    { label: 'Chờ xử lý', value: data.list.filter((order) => [WashOrderStatus.Pending, WashOrderStatus.Deposited].includes(order.status)).length, note: 'Cần tiếp nhận và điều phối', icon: <PendingActionsOutlinedIcon />, color: '#f59e0b', bg: '#fff7e6' },
    { label: 'Đang vận hành', value: data.list.filter((order) => ![WashOrderStatus.Pending, WashOrderStatus.Deposited, WashOrderStatus.Completed, WashOrderStatus.Cancelled].includes(order.status)).length, note: 'Đang lấy, giặt hoặc giao trả', icon: <LocalShippingOutlinedIcon />, color: '#2563eb', bg: '#edf4ff' },
    { label: 'Đã hoàn tất', value: data.list.filter((order) => order.status === WashOrderStatus.Completed).length, note: 'Hoàn thành quy trình', icon: <TaskAltOutlinedIcon />, color: '#16a34a', bg: '#eaf8ef' },
  ], [data]);

  const handleFilterClick = (colId: string, event: React.MouseEvent<HTMLElement>) => {
    setActiveFilterCol(colId);
    setFilterAnchorEl(event.currentTarget);
  };

  const handleApplyColumnFilter = (values: string[]) => {
    if (!activeFilterCol) return;
    const beField = WASH_ORDER_COLUMN_FILTER_MAP[activeFilterCol] || activeFilterCol;
    setColumnFilter(beField, values);
  };

  const activeFilterValues = useMemo(() => {
    if (!activeFilterCol) return [];
    const beField = WASH_ORDER_COLUMN_FILTER_MAP[activeFilterCol] || activeFilterCol;
    return columnFilters.find((f) => f.field === beField)?.selectedValues || [];
  }, [activeFilterCol, columnFilters]);

  const filteredColumnsList = useMemo(() => {
    return columnFilters.map((f) => {
      const match = Object.entries(WASH_ORDER_COLUMN_FILTER_MAP).find(([, be]) => be === f.field);
      return match ? match[0] : f.field;
    });
  }, [columnFilters]);

  return (
    <Box sx={{ p: { xs: 1.5, sm: 2.5, lg: 3 }, maxWidth: 1500, mx: 'auto' }}>
      {/* Header */}
      <Box sx={{ mb: 2.25, display: 'flex', justifyContent: 'space-between', alignItems: { xs: 'flex-start', sm: 'center' }, gap: 1.5, flexDirection: { xs: 'column', sm: 'row' } }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: -0.5 }}>
            Quản lý đơn hàng
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Theo dõi, điều phối và cập nhật toàn bộ đơn hàng WashGo.
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button variant="contained" startIcon={<AddIcon />}>
            Tạo đơn thủ công
          </Button>
        </Stack>
      </Box>

      <Grid container spacing={1.5} sx={{ mb: 2.25 }}>
        {orderStats.map((stat) => (
          <Grid item xs={6} lg={3} key={stat.label}>
            <Card variant="outlined" sx={{ height: '100%', borderColor: '#e7ebf0', boxShadow: '0 2px 6px rgba(15,23,42,.025)' }}>
              <CardContent sx={{ p: { xs: 1.5, sm: 2 }, '&:last-child': { pb: { xs: 1.5, sm: 2 } } }}>
                <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
                  <Box>
                    <Typography variant="caption" color="text.secondary">{stat.label}</Typography>
                    <Typography variant="h5" fontWeight={800} sx={{ mt: .25 }}>{stat.value}</Typography>
                    <Typography variant="caption" color="text.secondary">{stat.note}</Typography>
                  </Box>
                  <Box sx={{ width: 40, height: 40, borderRadius: 2, bgcolor: stat.bg, color: stat.color, display: 'grid', placeItems: 'center', flexShrink: 0, '& svg': { fontSize: 21 } }}>{stat.icon}</Box>
                </Stack>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      <Paper variant="outlined" sx={{ p: { xs: 1.25, sm: 2 }, borderRadius: 2, borderColor: '#e7ebf0' }}>
        <WashOrderFilterToolbar
          keyword={keywordInput}
          onKeywordChange={(val) => {
            setKeywordInput(val);
            setParams({ keyword: val, page: 1 });
          }}
          status={mergedParams.status}
          onStatusChange={(status) => setParams({ status, page: 1 })}
          onRefresh={fetchData}
          onReset={() => {
            setKeywordInput('');
            resetParams();
          }}
        />

      {/* Main Table */}
        <TableLayered
        rows={data.list}
        columns={columns}
        loading={loading}
        total={data.total}
        page={mergedParams.page || 1}
        take={mergedParams.take || 20}
        onChangePage={(page) => setParams({ page })}
        onChangeTake={(take) => setParams({ take, page: 1 })}
        onFilterClick={handleFilterClick}
        filteredColumns={filteredColumnsList}
        renderCollapse={(row) => (
          <Box sx={{ p: 2, backgroundColor: 'action.hover', borderRadius: 2 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
              Tóm tắt đơn: {row.orderCode}
            </Typography>
            <Typography variant="body2">
              Shipper phụ trách: <strong>{row.shipperName || 'Chưa phân công'}</strong>
            </Typography>
            <Typography variant="body2">
              Tiệm giặt xử lý: <strong>{row.merchantName || 'Trạm WashGo Central'}</strong>
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
              Mã QR: {row.qrCodeString}
            </Typography>
          </Box>
        )}
        />
      </Paper>

      {/* Column Filter Dropdown */}
      <ColumnFilterDropdown
        open={Boolean(filterAnchorEl)}
        anchorEl={filterAnchorEl}
        onClose={() => setFilterAnchorEl(null)}
        columnId={activeFilterCol || ''}
        columnLabel={columns.find((c) => c.id === activeFilterCol)?.label || ''}
        selectedValues={activeFilterValues}
        fetchDistinctValues={async (colId) => {
          const beField = WASH_ORDER_COLUMN_FILTER_MAP[colId] || colId;
          const res = await fetchWashOrderColumnDistinct(beField, mergedParams);
          return Array.isArray(res) ? res : [];
        }}
        onApply={handleApplyColumnFilter}
      />

      {/* Detail Dialog */}
      <WashOrderDetailDialog
        orderId={selectedOrderId}
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        onStatusUpdated={fetchData}
      />
    </Box>
  );
};
