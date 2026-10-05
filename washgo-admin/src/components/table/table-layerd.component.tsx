import React, { useState } from 'react';
import {
  Box,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  TablePagination,
  IconButton,
  Collapse,
  Checkbox,
  Skeleton,
  Typography,
  TableSortLabel,
  Tooltip,
} from '@mui/material';
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown';
import KeyboardArrowUpIcon from '@mui/icons-material/KeyboardArrowUp';
import FilterAltIcon from '@mui/icons-material/FilterAlt';
import FilterAltOutlinedIcon from '@mui/icons-material/FilterAltOutlined';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { TableLayeredProps, Column } from './table.interface';

export const TableLayered = <T extends Record<string, any>>({
  rows,
  columns,
  loading = false,
  total = 0,
  page = 1,
  take = 20,
  onChangePage,
  onChangeTake,
  sortBy = [],
  onSort,
  renderCollapse,
  rowKey = (row) => row.id || row.Id || String(Math.random()),
  selectedIds = [],
  onSelectRows,
  emptyText = 'Không tìm thấy dữ liệu phù hợp',
  onFilterClick,
  filteredColumns = [],
}: TableLayeredProps<T>) => {
  const [openRows, setOpenRows] = useState<Record<string, boolean>>({});

  const toggleRow = (id: string) => {
    setOpenRows((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  const handleSelectAll = (event: React.ChangeEvent<HTMLInputElement>) => {
    if (!onSelectRows) return;
    if (event.target.checked) {
      const allIds = rows.map((r) => rowKey(r));
      onSelectRows(allIds);
    } else {
      onSelectRows([]);
    }
  };

  const handleSelectRow = (id: string) => {
    if (!onSelectRows) return;
    if (selectedIds.includes(id)) {
      onSelectRows(selectedIds.filter((item) => item !== id));
    } else {
      onSelectRows([...selectedIds, id]);
    }
  };

  const isAllSelected = rows.length > 0 && selectedIds.length === rows.length;
  const isIndeterminate = selectedIds.length > 0 && selectedIds.length < rows.length;

  return (
    <Paper
      elevation={0}
      sx={{
        width: '100%',
        overflow: 'hidden',
        border: '1px solid',
        borderColor: 'divider',
        borderRadius: 2,
        backgroundColor: 'background.paper',
      }}
    >
      <TableContainer sx={{ maxHeight: 'calc(100vh - 280px)', minHeight: 320 }}>
        <Table stickyHeader size="small" sx={{ minWidth: 700 }}>
          <TableHead>
            <TableRow>
              {renderCollapse && (
                <TableCell padding="checkbox" sx={{ width: 48, backgroundColor: 'action.hover' }} />
              )}
              {onSelectRows && (
                <TableCell padding="checkbox" sx={{ width: 48, backgroundColor: 'action.hover' }}>
                  <Checkbox
                    size="small"
                    indeterminate={isIndeterminate}
                    checked={isAllSelected}
                    onChange={handleSelectAll}
                  />
                </TableCell>
              )}
              {columns.map((col) => {
                const isSorted = sortBy.some((s) => s.field === col.id);
                const isDesc = sortBy.find((s) => s.field === col.id)?.isDescending ?? false;
                const isFiltered = filteredColumns.includes(col.id);

                return (
                  <TableCell
                    key={col.id}
                    align={col.align || 'left'}
                    sx={{
                      minWidth: col.minWidth,
                      width: col.width,
                      fontWeight: 700,
                      fontSize: '0.8125rem',
                      color: 'text.primary',
                      backgroundColor: 'action.hover',
                      whiteSpace: 'nowrap',
                      userSelect: 'none',
                    }}
                  >
                    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
                      {col.sortable && onSort ? (
                        <TableSortLabel
                          active={isSorted}
                          direction={isDesc ? 'desc' : 'asc'}
                          onClick={() => onSort(col.id)}
                        >
                          {col.label}
                        </TableSortLabel>
                      ) : (
                        <span>{col.label}</span>
                      )}

                      {col.filterable && onFilterClick && (
                        <Tooltip title={`Lọc theo ${col.label}`}>
                          <IconButton
                            size="small"
                            onClick={(e) => onFilterClick(col.id, e)}
                            sx={{
                              p: 0.25,
                              color: isFiltered ? 'primary.main' : 'text.disabled',
                              backgroundColor: isFiltered ? 'primary.light' : 'transparent',
                            }}
                          >
                            {isFiltered ? (
                              <FilterAltIcon sx={{ fontSize: 16 }} />
                            ) : (
                              <FilterAltOutlinedIcon sx={{ fontSize: 16 }} />
                            )}
                          </IconButton>
                        </Tooltip>
                      )}
                    </Box>
                  </TableCell>
                );
              })}
            </TableRow>
          </TableHead>

          <TableBody>
            {loading ? (
              Array.from({ length: 6 }).map((_, rIdx) => (
                <TableRow key={rIdx}>
                  {renderCollapse && <TableCell sx={{ p: 1 }}><Skeleton variant="circular" width={24} height={24} /></TableCell>}
                  {onSelectRows && <TableCell sx={{ p: 1 }}><Skeleton variant="rectangular" width={20} height={20} /></TableCell>}
                  {columns.map((col, cIdx) => (
                    <TableCell key={cIdx} sx={{ p: 1.5 }}>
                      <Skeleton variant="text" width={cIdx === 0 ? '70%' : '50%'} height={24} />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : rows.length === 0 ? (
              <TableRow>
                <TableCell
                  colSpan={columns.length + (renderCollapse ? 1 : 0) + (onSelectRows ? 1 : 0)}
                  align="center"
                  sx={{ py: 8 }}
                >
                  <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1 }}>
                    <InboxOutlinedIcon sx={{ fontSize: 48, color: 'text.disabled' }} />
                    <Typography variant="body2" color="text.secondary">
                      {emptyText}
                    </Typography>
                  </Box>
                </TableCell>
              </TableRow>
            ) : (
              rows.map((row, index) => {
                const key = rowKey(row);
                const isOpen = !!openRows[key];
                const isSelected = selectedIds.includes(key);

                return (
                  <React.Fragment key={key}>
                    <TableRow
                      hover
                      selected={isSelected}
                      sx={{
                        '&:hover': { backgroundColor: 'action.hover' },
                        transition: 'background-color 0.15s ease',
                      }}
                    >
                      {renderCollapse && (
                        <TableCell padding="checkbox">
                          <IconButton size="small" onClick={() => toggleRow(key)}>
                            {isOpen ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                          </IconButton>
                        </TableCell>
                      )}
                      {onSelectRows && (
                        <TableCell padding="checkbox">
                          <Checkbox
                            size="small"
                            checked={isSelected}
                            onChange={() => handleSelectRow(key)}
                          />
                        </TableCell>
                      )}
                      {columns.map((col) => (
                        <TableCell
                          key={col.id}
                          align={col.align || 'left'}
                          sx={{ fontSize: '0.84rem', py: 1.25 }}
                        >
                          {col.render ? col.render(row, index) : row[col.id] ?? '-'}
                        </TableCell>
                      ))}
                    </TableRow>

                    {renderCollapse && (
                      <TableRow>
                        <TableCell
                          colSpan={columns.length + 1 + (onSelectRows ? 1 : 0)}
                          sx={{ py: 0, px: 2, borderBottom: isOpen ? undefined : 'none' }}
                        >
                          <Collapse in={isOpen} timeout="auto" unmountOnExit>
                            <Box sx={{ py: 2 }}>{renderCollapse(row)}</Box>
                          </Collapse>
                        </TableCell>
                      </TableRow>
                    )}
                  </React.Fragment>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Pagination Footer */}
      <TablePagination
        component="div"
        count={total}
        page={Math.max(0, page - 1)}
        rowsPerPage={take}
        rowsPerPageOptions={[10, 20, 50, 100]}
        onPageChange={(_, newPage) => onChangePage && onChangePage(newPage + 1)}
        onRowsPerPageChange={(e) => onChangeTake && onChangeTake(parseInt(e.target.value, 10))}
        labelRowsPerPage="Số dòng/trang:"
        labelDisplayedRows={({ from, to, count }) => `${from}–${to} trên ${count !== -1 ? count : `hơn ${to}`}`}
        sx={{ borderTop: '1px solid', borderColor: 'divider' }}
      />
    </Paper>
  );
};
