import QRCode from "qrcode";

const qrOptions: QRCode.QRCodeToDataURLOptions = {
  errorCorrectionLevel: "M",
  margin: 2,
  width: 320,
  color: {
    dark: "#111827",
    light: "#ffffff"
  }
};

/**
 * Generates a QR image for the public short URL only. Callers should pass the
 * URL returned by the authorized short-link API, never a destination or secret.
 */
export async function createShortLinkQrDataUrl(shortUrl: string): Promise<string> {
  return QRCode.toDataURL(getShortLinkQrPayload(shortUrl), qrOptions);
}

export function getShortLinkQrPayload(shortUrl: string): string {
  const normalizedUrl = shortUrl.trim();
  if (!normalizedUrl) {
    throw new Error("A short URL is required to generate a QR code.");
  }

  return normalizedUrl;
}

export function downloadShortLinkQr(dataUrl: string, code: string): void {
  const anchor = document.createElement("a");
  anchor.href = dataUrl;
  anchor.download = `short-link-${code}.png`;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
}
