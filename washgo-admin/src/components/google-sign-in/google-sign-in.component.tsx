import React, { useEffect, useRef, useState } from 'react';
import { Alert, Box } from '@mui/material';

interface GoogleCredentialResponse {
  credential?: string;
}

interface GoogleIdentityApi {
  accounts: {
    id: {
      initialize: (config: { client_id: string; callback: (response: GoogleCredentialResponse) => void }) => void;
      renderButton: (parent: HTMLElement, options: Record<string, string | number>) => void;
    };
  };
}

declare global {
  interface Window {
    google?: GoogleIdentityApi;
  }
}

interface GoogleSignInButtonProps {
  onCredential: (idToken: string) => void;
  disabled?: boolean;
}

const GOOGLE_SCRIPT_URL = 'https://accounts.google.com/gsi/client';

export const GoogleSignInButton: React.FC<GoogleSignInButtonProps> = ({ onCredential, disabled = false }) => {
  const buttonRef = useRef<HTMLDivElement>(null);
  const credentialHandlerRef = useRef(onCredential);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    credentialHandlerRef.current = onCredential;
  }, [onCredential]);

  useEffect(() => {
    const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;
    if (!clientId) {
      setError('Google Sign-In chưa được cấu hình cho giao diện này.');
      return;
    }

    const renderGoogleButton = () => {
      if (!window.google || !buttonRef.current) return;

      buttonRef.current.replaceChildren();
      window.google.accounts.id.initialize({
        client_id: clientId,
        callback: (response) => {
          if (response.credential) credentialHandlerRef.current(response.credential);
          else setError('Không nhận được Google ID token. Vui lòng thử lại.');
        },
      });
      window.google.accounts.id.renderButton(buttonRef.current, {
        type: 'standard',
        theme: 'outline',
        size: 'large',
        text: 'signin_with',
        shape: 'rectangular',
        width: Math.min(buttonRef.current.clientWidth || 360, 400),
      });
    };

    const existingScript = document.querySelector<HTMLScriptElement>(`script[src="${GOOGLE_SCRIPT_URL}"]`);
    if (existingScript) {
      if (window.google) renderGoogleButton();
      else existingScript.addEventListener('load', renderGoogleButton, { once: true });
      return () => existingScript.removeEventListener('load', renderGoogleButton);
    }

    const script = document.createElement('script');
    script.src = GOOGLE_SCRIPT_URL;
    script.async = true;
    script.defer = true;
    script.onload = renderGoogleButton;
    script.onerror = () => setError('Không thể tải Google Sign-In. Vui lòng kiểm tra kết nối mạng.');
    document.head.appendChild(script);

    return () => {
      script.onload = null;
      script.onerror = null;
    };
  }, []);

  return (
    <Box sx={{ opacity: disabled ? 0.55 : 1, pointerEvents: disabled ? 'none' : 'auto' }}>
      <Box ref={buttonRef} sx={{ minHeight: 40, display: 'flex', justifyContent: 'center' }} />
      {error && <Alert severity="warning" sx={{ mt: 1.5 }}>{error}</Alert>}
    </Box>
  );
};
