import React, { useState, useEffect } from 'react';
import {
  Popover,
  Box,
  Typography,
  TextField,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Checkbox,
  Button,
  CircularProgress,
  Divider,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { ColumnFilterDistinctValueDto } from '../../common/interfaces/api.interface';

interface ColumnFilterDropdownProps {
  anchorEl: HTMLElement | null;
  open: boolean;
  onClose: () => void;
  columnId: string;
  columnLabel: string;
  selectedValues: string[];
  fetchDistinctValues: (columnId: string) => Promise<ColumnFilterDistinctValueDto[]>;
  onApply: (values: string[]) => void;
}

export const ColumnFilterDropdown: React.FC<ColumnFilterDropdownProps> = ({
  anchorEl,
  open,
  onClose,
  columnId,
  columnLabel,
  selectedValues,
  fetchDistinctValues,
  onApply,
}) => {
  const [loading, setLoading] = useState(false);
  const [options, setOptions] = useState<ColumnFilterDistinctValueDto[]>([]);
  const [tempSelected, setTempSelected] = useState<string[]>([]);
  const [searchText, setSearchText] = useState('');

  useEffect(() => {
    if (open) {
      setTempSelected(selectedValues || []);
      setSearchText('');
      setLoading(true);
      fetchDistinctValues(columnId)
        .then((data) => {
          setOptions(Array.isArray(data) ? data : []);
        })
        .catch(() => setOptions([]))
        .finally(() => setLoading(false));
    }
  }, [open, columnId, selectedValues, fetchDistinctValues]);

  const handleToggle = (val: string) => {
    setTempSelected((prev) =>
      prev.includes(val) ? prev.filter((v) => v !== val) : [...prev, val]
    );
  };

  const handleSelectAll = () => {
    if (tempSelected.length === filteredOptions.length) {
      setTempSelected([]);
    } else {
      setTempSelected(filteredOptions.map((o) => String(o.value)));
    }
  };

  const filteredOptions = options.filter((opt) =>
    String(opt.label || opt.value)
      .toLowerCase()
      .includes(searchText.toLowerCase())
  );

  return (
    <Popover
      open={open}
      anchorEl={anchorEl}
      onClose={onClose}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
      transformOrigin={{ vertical: 'top', horizontal: 'left' }}
      PaperProps={{
        sx: {
          width: 280,
          maxHeight: 400,
          display: 'flex',
          flexDirection: 'column',
          boxShadow: '0 8px 24px rgba(0,0,0,0.12)',
          borderRadius: 2,
        },
      }}
    >
      <Box sx={{ p: 1.5, pb: 1 }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
          Lọc theo: {columnLabel}
        </Typography>
        <TextField
          size="small"
          fullWidth
          placeholder="Tìm giá trị..."
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          InputProps={{
            startAdornment: <SearchIcon sx={{ fontSize: 18, color: 'text.secondary', mr: 0.5 }} />,
          }}
        />
      </Box>

      <Divider />

      <Box sx={{ flex: 1, overflowY: 'auto', minHeight: 120 }}>
        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress size={24} />
          </Box>
        ) : filteredOptions.length === 0 ? (
          <Box sx={{ p: 2, textAlign: 'center' }}>
            <Typography variant="caption" color="text.secondary">
              Không có dữ liệu
            </Typography>
          </Box>
        ) : (
          <List dense disablePadding>
            <ListItem disablePadding>
              <ListItemButton onClick={handleSelectAll} sx={{ py: 0.5 }}>
                <ListItemIcon sx={{ minWidth: 32 }}>
                  <Checkbox
                    size="small"
                    checked={
                      filteredOptions.length > 0 &&
                      filteredOptions.every((o) => tempSelected.includes(String(o.value)))
                    }
                    indeterminate={
                      tempSelected.length > 0 &&
                      tempSelected.length < filteredOptions.length
                    }
                  />
                </ListItemIcon>
                <ListItemText
                  primary={<Typography variant="body2" sx={{ fontWeight: 600 }}>(Chọn tất cả)</Typography>}
                />
              </ListItemButton>
            </ListItem>
            {filteredOptions.map((opt) => {
              const valStr = String(opt.value);
              const isChecked = tempSelected.includes(valStr);
              return (
                <ListItem key={valStr} disablePadding>
                  <ListItemButton onClick={() => handleToggle(valStr)} sx={{ py: 0.25 }}>
                    <ListItemIcon sx={{ minWidth: 32 }}>
                      <Checkbox size="small" checked={isChecked} />
                    </ListItemIcon>
                    <ListItemText
                      primary={
                        <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                          <Typography variant="body2" noWrap sx={{ maxWidth: 160 }}>
                            {opt.label || valStr}
                          </Typography>
                          {opt.count !== undefined && (
                            <Typography variant="caption" color="text.secondary">
                              ({opt.count})
                            </Typography>
                          )}
                        </Box>
                      }
                    />
                  </ListItemButton>
                </ListItem>
              );
            })}
          </List>
        )}
      </Box>

      <Divider />

      <Box sx={{ p: 1, display: 'flex', justifyContent: 'flex-end', gap: 1 }}>
        <Button
          size="small"
          onClick={() => {
            setTempSelected([]);
            onApply([]);
            onClose();
          }}
        >
          Xóa lọc
        </Button>
        <Button
          size="small"
          variant="contained"
          onClick={() => {
            onApply(tempSelected);
            onClose();
          }}
        >
          Áp dụng ({tempSelected.length})
        </Button>
      </Box>
    </Popover>
  );
};
