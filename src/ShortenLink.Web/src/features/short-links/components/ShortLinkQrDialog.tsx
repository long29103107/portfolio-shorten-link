import { useEffect, useState } from "react";
import { Button } from "../../../shared/components/ui/button";
import type { ShortLinkAdminItem } from "../types";
import { createShortLinkQrDataUrl, downloadShortLinkQr } from "../domain/qr";

type ShortLinkQrDialogProps = {
  link: ShortLinkAdminItem | null;
  onClose: () => void;
};

export function ShortLinkQrDialog({ link, onClose }: ShortLinkQrDialogProps) {
  const [dataUrl, setDataUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [retryCount, setRetryCount] = useState(0);

  useEffect(() => {
    if (!link) {
      setDataUrl(null);
      setError(null);
      setIsLoading(false);
      return;
    }

    let cancelled = false;
    setDataUrl(null);
    setError(null);
    setIsLoading(true);

    void createShortLinkQrDataUrl(link.shortUrl)
      .then((nextDataUrl) => {
        if (!cancelled) {
          setDataUrl(nextDataUrl);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setError("The QR code could not be generated.");
        }
      })
      .finally(() => {
        if (!cancelled) {
          setIsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [link, retryCount]);

  if (!link) return null;

  return (
    <div className="dialog-backdrop" role="presentation">
      <div
        className="qr-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="qr-dialog-title"
      >
        <div className="analytics-dialog-header">
          <div>
            <p className="eyebrow">Share</p>
            <h2 id="qr-dialog-title">QR code for {link.code}</h2>
          </div>
          <Button variant="secondary" onClick={onClose}>Close</Button>
        </div>

        <div className="qr-dialog-content">
          {isLoading ? <div className="qr-loading" aria-live="polite">Generating QR code...</div> : null}
          {!isLoading && error ? (
            <div className="qr-error" role="alert">
              <p>{error}</p>
              <Button variant="secondary" onClick={() => setRetryCount((count) => count + 1)}>Retry</Button>
            </div>
          ) : null}
          {!isLoading && !error && dataUrl ? (
            <img
              className="short-link-qr"
              src={dataUrl}
              alt={`QR code for ${link.shortUrl}`}
            />
          ) : null}
          <p className="muted-copy">Scan this code to open the short link.</p>
          <code className="qr-short-url">{link.shortUrl}</code>
        </div>

        <div className="dialog-actions">
          <Button
            variant="secondary"
            disabled={!dataUrl || isLoading}
            onClick={() => dataUrl && downloadShortLinkQr(dataUrl, link.code)}
          >
            Download PNG
          </Button>
          <Button onClick={onClose}>Done</Button>
        </div>
      </div>
    </div>
  );
}
