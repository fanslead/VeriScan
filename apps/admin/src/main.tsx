import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '@douyinfe/semi-ui/lib/es/react19-adapter';
import { App } from './app/App';
import './styles/tokens.css';
import './styles/global.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
