/**
 * Shrinks a phone photo / scan before it is uploaded (collections soft copies, user 2026-11-21): JPG and PNG are scaled to
 * fit 1600 px on the long side and saved as JPEG (quality 0.8) — a 4–8 MB photo ends up a few hundred KB and stays
 * readable. PDFs and anything that can't be read as an image go up unchanged, as does an image that wouldn't get smaller.
 */
export async function compressUpload(file: File, maxSide = 1600, quality = 0.8): Promise<File> {
  if (!/^image\/(png|jpeg)$/.test(file.type) && !/\.(jpe?g|png)$/i.test(file.name)) return file;
  try {
    const img = await loadImage(file);
    const scale = Math.min(1, maxSide / Math.max(img.naturalWidth, img.naturalHeight));
    const w = Math.max(1, Math.round(img.naturalWidth * scale));
    const h = Math.max(1, Math.round(img.naturalHeight * scale));
    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    const ctx = canvas.getContext('2d')!;
    ctx.imageSmoothingQuality = 'high';
    ctx.fillStyle = '#fff';   // JPEG has no transparency
    ctx.fillRect(0, 0, w, h);
    ctx.drawImage(img, 0, 0, w, h);
    const blob = await new Promise<Blob | null>(r => canvas.toBlob(r, 'image/jpeg', quality));
    if (!blob || blob.size >= file.size) return file;
    const base = file.name.replace(/\.[^.]+$/, '') || 'scan';
    return new File([blob], `${base}.jpg`, { type: 'image/jpeg', lastModified: Date.now() });
  } catch {
    return file;
  }
}

function loadImage(file: File): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => { URL.revokeObjectURL(url); resolve(img); };
    img.onerror = () => { URL.revokeObjectURL(url); reject(new Error('not an image')); };
    img.src = url;
  });
}
