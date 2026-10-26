/**
 * Shrinks an image in the browser before upload (logos): scaled to fit maxW × maxH, then PNG
 * (keeps transparency) or JPEG (photos / when the PNG is still big). A typical logo ends up a few
 * tens of KB. Returns the new file, its type and size, plus the original size for the message.
 */
export interface ShrunkImage {
  blob: Blob;
  type: 'image/png' | 'image/jpeg';
  fileName: string;
  originalKb: number;
  kb: number;
}

/** Bytes → KB with 1 decimal ("38.4"). */
export const toKb = (bytes: number) => Math.round(bytes / 102.4) / 10;

export async function shrinkImage(file: File, maxW = 600, maxH = 240, maxPngBytes = 150 * 1024): Promise<ShrunkImage> {
  if (!/^image\/(png|jpeg)$/.test(file.type)) throw new Error('Only PNG or JPG images can be used.');
  const img = await loadImage(file);
  const scale = Math.min(1, maxW / img.naturalWidth, maxH / img.naturalHeight);
  const w = Math.max(1, Math.round(img.naturalWidth * scale));
  const h = Math.max(1, Math.round(img.naturalHeight * scale));

  const canvas = document.createElement('canvas');
  canvas.width = w;
  canvas.height = h;
  const ctx = canvas.getContext('2d')!;
  ctx.imageSmoothingQuality = 'high';

  const base = file.name.replace(/\.[^.]+$/, '') || 'logo';
  if (file.type === 'image/png') {
    ctx.drawImage(img, 0, 0, w, h);
    const png = await toBlob(canvas, 'image/png');
    if (png.size <= maxPngBytes)
      return { blob: png, type: 'image/png', fileName: `${base}.png`, originalKb: toKb(file.size), kb: toKb(png.size) };
    ctx.clearRect(0, 0, w, h);
  }
  // JPEG has no transparency: paint a white background first.
  ctx.fillStyle = '#fff';
  ctx.fillRect(0, 0, w, h);
  ctx.drawImage(img, 0, 0, w, h);
  const jpg = await toBlob(canvas, 'image/jpeg', 0.85);
  return { blob: jpg, type: 'image/jpeg', fileName: `${base}.jpg`, originalKb: toKb(file.size), kb: toKb(jpg.size) };
}

function loadImage(file: File): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => { URL.revokeObjectURL(url); resolve(img); };
    img.onerror = () => { URL.revokeObjectURL(url); reject(new Error('That file could not be read as an image.')); };
    img.src = url;
  });
}

function toBlob(canvas: HTMLCanvasElement, type: string, quality?: number): Promise<Blob> {
  return new Promise((resolve, reject) =>
    canvas.toBlob(b => (b ? resolve(b) : reject(new Error('Could not convert the image.'))), type, quality));
}
