import { Box, LinearProgress, Typography } from '@mui/material';
import { useTheme } from '@mui/material/styles';

interface ConfidenceBarProps {
  value: number;
}

function getConfidenceColor(pct: number): string {
  if (pct >= 70) return '#00e676';
  if (pct >= 40) return '#ffb300';
  return '#ff1744';
}

export default function ConfidenceBar({ value }: ConfidenceBarProps) {
  const theme = useTheme();
  const percentage = Math.round(value * 100);
  const color = getConfidenceColor(percentage);

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
      <LinearProgress
        variant="determinate"
        value={percentage}
        sx={{
          flex: 1,
          height: 8,
          borderRadius: 4,
          backgroundColor: 'rgba(255, 255, 255, 0.06)',
          '& .MuiLinearProgress-bar': {
            backgroundColor: color,
            borderRadius: 4,
          },
        }}
      />
      <Typography
        variant="caption"
        sx={{
          minWidth: 36,
          fontFamily: theme.fontFamilyMono,
          fontWeight: 500,
          color,
        }}
      >
        {percentage}%
      </Typography>
    </Box>
  );
}
