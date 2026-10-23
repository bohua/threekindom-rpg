import fs from "node:fs";
import { PNG } from "pngjs";

export interface PngInfo {
  width: number;
  height: number;
  hasAlpha: boolean;
  /** Every alpha value is 0 or 255 (only meaningful when hasAlpha). */
  binaryAlpha: boolean;
  /** Fraction of pixels that are fully transparent. */
  transparentRatio: number;
}

const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

export function isPng(file: string): boolean {
  const fd = fs.openSync(file, "r");
  try {
    const head = Buffer.alloc(8);
    fs.readSync(fd, head, 0, 8, 0);
    return head.equals(SIGNATURE);
  } finally {
    fs.closeSync(fd);
  }
}

export function inspectPng(file: string): PngInfo {
  const png = PNG.sync.read(fs.readFileSync(file));
  // pngjs always decodes to RGBA; colorType 4/6 means the file itself carries alpha.
  const meta = png as unknown as { colorType?: number; alpha?: boolean };
  const hasAlpha = meta.alpha === true || meta.colorType === 4 || meta.colorType === 6;
  let binary = true;
  let transparent = 0;
  const data = png.data;
  for (let i = 3; i < data.length; i += 4) {
    const a = data[i];
    if (a === 0) transparent++;
    else if (a !== 255) binary = false;
  }
  return {
    width: png.width,
    height: png.height,
    hasAlpha,
    binaryAlpha: binary,
    transparentRatio: transparent / (png.width * png.height),
  };
}

/** Nearest-neighbour resample so the longest side is at most maxSide (pixel art stays crisp). */
export function previewPng(file: string, maxSide: number): Buffer {
  const src = PNG.sync.read(fs.readFileSync(file));
  const scale = Math.min(1, maxSide / Math.max(src.width, src.height));
  if (scale === 1) return fs.readFileSync(file);
  const w = Math.max(1, Math.round(src.width * scale));
  const h = Math.max(1, Math.round(src.height * scale));
  const dst = new PNG({ width: w, height: h });
  for (let y = 0; y < h; y++) {
    const sy = Math.min(src.height - 1, Math.floor(y / scale));
    for (let x = 0; x < w; x++) {
      const sx = Math.min(src.width - 1, Math.floor(x / scale));
      const si = (sy * src.width + sx) * 4;
      const di = (y * w + x) * 4;
      src.data.copy(dst.data, di, si, si + 4);
    }
  }
  return PNG.sync.write(dst);
}
