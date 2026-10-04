import type { FastifyRequest } from 'fastify';
import { ApiError } from '../errors';
import { savePartToTemp, type TempFile } from './upload';

/**
 * 表单读取工具
 *
 * Unity 侧 CloudSaveClient 用的是 WWWForm → 实际是 multipart/form-data；
 * 网页/其他客户端可能用 JSON 或 x-www-form-urlencoded。
 * 这里统一成 { fields, files }，让业务层不用关心编码格式。
 */

export interface MultipartReadResult {
  fields: Record<string, string>;
  files: Record<string, TempFile>;
}

export interface MultipartReadOptions {
  /**
   * 允许的文件字段名 → 该字段的大小上限（字节）。
   * 未列出的文件字段会被丢弃（同时排空流，避免 busboy 卡住）。
   */
  fileFields: Record<string, number>;
  maxFields?: number;
}

function drain(stream: NodeJS.ReadableStream): void {
  const readable = stream as unknown as { resume?: () => void };
  if (typeof readable.resume === 'function') readable.resume();
}

function bodyToFields(body: unknown): Record<string, string> {
  if (body == null) return {};
  if (typeof body !== 'object') return {};
  const out: Record<string, string> = {};
  for (const [k, v] of Object.entries(body as Record<string, unknown>)) {
    if (v == null) continue;
    out[k] = typeof v === 'string' ? v : String(v);
  }
  return out;
}

/**
 * 读取请求中的表单字段与文件（文件一律先落盘，避免大文件驻留内存）
 */
export async function readMultipart(
  req: FastifyRequest,
  opts: MultipartReadOptions,
): Promise<MultipartReadResult> {
  if (!req.isMultipart()) {
    return { fields: bodyToFields(req.body), files: {} };
  }

  const limits = Object.values(opts.fileFields);
  const maxFileSize = limits.length > 0 ? Math.max(...limits) : 1024;

  const parts = req.parts({ limits: { fileSize: maxFileSize, fields: opts.maxFields ?? 60 } });
  const fields: Record<string, string> = {};
  const files: Record<string, TempFile> = {};

  try {
    for await (const part of parts) {
      if (part.type === 'file') {
        const maxBytes = opts.fileFields[part.fieldname];
        if (maxBytes == null) {
          drain(part.file);
          continue;
        }
        files[part.fieldname] = await savePartToTemp(part, maxBytes);
      } else {
        fields[part.fieldname] = String(part.value ?? '');
      }
    }
  } catch (e) {
    throw e;
  }

  return { fields, files };
}

/** 读取登录/注册所需凭证（兼容 multipart / urlencoded / json 三种形态） */
export async function readCredentials(req: FastifyRequest): Promise<{ username: string; password: string }> {
  const { fields } = await readMultipart(req, { fileFields: {}, maxFields: 10 });
  const username = (fields.username ?? '').trim();
  const password = fields.password ?? '';

  if (username === '' || password === '') {
    throw ApiError.badRequest('用户名与密码不能为空');
  }
  return { username, password };
}
