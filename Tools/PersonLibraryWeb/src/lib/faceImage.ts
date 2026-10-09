/**
 * 文件名：faceImage.ts
 * 描述：自定义头像相关的浏览器端图片处理工具（纯 Canvas，无第三方依赖）。
 *
 *       用途：
 *       - 批量导入半身像时，把用户拖入的「原图」自动裁成 240×240 半身像；
 *       - 半身像自动生成 64×80 小头像（默认取头部区域，之后可在编辑器里微调）；
 *       - 供编辑器把 <img> 或 File 加载成已解码的 HTMLImageElement。
 *
 *       坐标系：所有裁切都以源图的自然像素（naturalWidth/naturalHeight）为准。
 */

/** 半身像输出尺寸 */
export const BUST_SIZE = { width: 240, height: 240 }

/** 小头像输出尺寸 */
export const FACE_SIZE = { width: 64, height: 80 }

/** 自动裁小头像时，相对半身像宽度的取值比例 */
const HEAD_WIDTH_RATIO = 0.52

/** 自动裁小头像时，顶部留白比例（避免切掉发顶） */
const HEAD_TOP_RATIO = 0.02

/** 单张图片允许的最大字节数（与后端保持一致，约 8MB） */
export const MAX_IMAGE_BYTES = 8 * 1024 * 1024

/**
 * 把数值限制在区间内。
 * @param value 数值
 * @param min 下限
 * @param max 上限
 * @returns 限制后的值
 */
function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

/**
 * 校验是否为可用的图片文件。
 * @param file 文件
 * @returns 是否可用
 */
export function isUsableImageFile(file: File): boolean {
  return Boolean(file) && file.type.startsWith('image/') && file.size <= MAX_IMAGE_BYTES
}

/**
 * 读取本地文件并解码为图片元素。
 * @param file 图片文件
 * @returns 已解码的图片元素
 */
export function loadImageFromFile(file: File): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => {
      const img = new Image()
      img.onload = () => resolve(img)
      img.onerror = () => reject(new Error('图片解析失败，请更换一张图片'))
      img.src = String(reader.result)
    }
    reader.onerror = () => reject(new Error('图片读取失败，请重试'))
    reader.readAsDataURL(file)
  })
}

/**
 * 加载图片地址（同源地址或 object URL）。
 * @param url 图片地址
 * @returns 已解码的图片元素
 */
export function loadImageFromUrl(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.onload = () => resolve(img)
    img.onerror = () => reject(new Error('图片加载失败，请稍后重试'))
    img.src = url
  })
}

/**
 * 裁切源图的一块矩形并缩放到目标尺寸，返回 PNG dataURL。
 * @param image 源图
 * @param sx 裁切起点 X（源图像素）
 * @param sy 裁切起点 Y（源图像素）
 * @param sw 裁切宽度（源图像素）
 * @param sh 裁切高度（源图像素）
 * @param dw 输出宽度
 * @param dh 输出高度
 * @returns PNG dataURL
 */
export function cropToDataUrl(
  image: HTMLImageElement,
  sx: number,
  sy: number,
  sw: number,
  sh: number,
  dw: number,
  dh: number,
): string {
  const canvas = document.createElement('canvas')
  canvas.width = dw
  canvas.height = dh
  const ctx = canvas.getContext('2d')
  if (!ctx) return ''
  ctx.drawImage(image, sx, sy, sw, sh, 0, 0, dw, dh)
  return canvas.toDataURL('image/png')
}

/**
 * 按「铺满 + 顶部对齐」把原图裁成目标尺寸，返回 PNG dataURL。
 *
 * 头像素材的人物头部通常位于画面上部，因此默认顶部对齐（裁掉下方多余部分），
 * 比居中裁切更不容易切掉头；宽度不足时左右居中裁切。
 * @param image 源图
 * @param width 输出宽度
 * @param height 输出高度
 * @param align 垂直对齐方式：top=顶部对齐（默认），center=居中
 * @returns PNG dataURL
 */
export function coverCropToDataUrl(
  image: HTMLImageElement,
  width: number,
  height: number,
  align: 'top' | 'center' = 'top',
): string {
  const sourceW = image.naturalWidth || 0
  const sourceH = image.naturalHeight || 0
  if (!sourceW || !sourceH) return ''

  // 以「铺满」为基准取裁切框，再按对齐方式放置
  const targetRatio = width / height
  const sourceRatio = sourceW / sourceH
  let cropW = sourceW
  let cropH = sourceH
  if (sourceRatio > targetRatio) {
    // 源图更宽：左右裁
    cropW = Math.round(sourceH * targetRatio)
  } else {
    // 源图更高：上下裁
    cropH = Math.round(sourceW / targetRatio)
  }
  const sx = Math.round((sourceW - cropW) / 2)
  const sy = align === 'top' ? 0 : Math.round((sourceH - cropH) / 2)
  return cropToDataUrl(image, sx, sy, cropW, cropH, width, height)
}

/**
 * 由半身像自动生成小头像（64×80）。
 *
 * 取半身像上部居中的一块区域（默认宽度为半身像的 52%），
 * 这是「头部大致位置」的经验值；批量导入时用作默认值，
 * 个别不满意的可以再用「编辑小头像」以半身像为基准手工微调。
 * @param bust 半身像（240×240 或任意尺寸）
 * @returns PNG dataURL
 */
export function autoHeadFromBust(bust: HTMLImageElement): string {
  const sourceW = bust.naturalWidth || 0
  const sourceH = bust.naturalHeight || 0
  if (!sourceW || !sourceH) return ''

  const cropW = clamp(Math.round(sourceW * HEAD_WIDTH_RATIO), 1, sourceW)
  const cropH = clamp(Math.round(cropW * (FACE_SIZE.height / FACE_SIZE.width)), 1, sourceH)
  const sx = Math.round((sourceW - cropW) / 2)
  const sy = clamp(Math.round(sourceH * HEAD_TOP_RATIO), 0, Math.max(0, sourceH - cropH))
  return cropToDataUrl(bust, sx, sy, cropW, cropH, FACE_SIZE.width, FACE_SIZE.height)
}
