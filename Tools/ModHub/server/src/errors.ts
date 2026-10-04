/**
 * 统一业务异常
 * 所有错误响应统一为 JSON: { error: "..." }，与现有云存档服务返回格式保持一致，
 * 便于 Unity 侧 JsonUtility.FromJson<CloudSaveApiError> 直接解析。
 */
export class ApiError extends Error {
  public readonly statusCode: number;
  public readonly code: string;
  /** 可选详情：例如 zip 扫描出的多条问题清单 */
  public readonly details?: string[];

  constructor(statusCode: number, message: string, code?: string, details?: string[]) {
    super(message);
    this.name = 'ApiError';
    this.statusCode = statusCode;
    this.code = code ?? 'error';
    this.details = details && details.length > 0 ? details : undefined;
  }

  static badRequest(message: string, details?: string[]): ApiError {
    return new ApiError(400, message, 'bad_request', details);
  }
  static unauthorized(message = '未登录或登录已失效'): ApiError {
    return new ApiError(401, message, 'unauthorized');
  }
  static forbidden(message = '没有权限执行该操作'): ApiError {
    return new ApiError(403, message, 'forbidden');
  }
  static notFound(message = '资源不存在'): ApiError {
    return new ApiError(404, message, 'not_found');
  }
  static conflict(message: string): ApiError {
    return new ApiError(409, message, 'conflict');
  }
  static payloadTooLarge(message: string): ApiError {
    return new ApiError(413, message, 'payload_too_large');
  }
}
