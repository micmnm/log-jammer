import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { ThemeProvider, CssBaseline } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import theme from './theme';
import Layout from './components/Layout';
import Dashboard from './pages/Dashboard';
import ErrorGroups from './pages/ErrorGroups';
import ErrorGroupDetail from './pages/ErrorGroupDetail';
import Alerts from './pages/Alerts';
import Classification from './pages/Classification';
import DataSources from './pages/DataSources';
import Settings from './pages/Settings';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5000,
      retry: 1,
    },
  },
});

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <BrowserRouter>
          <Routes>
            <Route element={<Layout />}>
              <Route path="/" element={<Dashboard />} />
              <Route path="/error-groups" element={<ErrorGroups />} />
              <Route path="/error-groups/:id" element={<ErrorGroupDetail />} />
              <Route path="/alerts" element={<Alerts />} />
              <Route path="/classification" element={<Classification />} />
              <Route path="/data-sources" element={<DataSources />} />
              <Route path="/settings" element={<Settings />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </ThemeProvider>
    </QueryClientProvider>
  );
}
