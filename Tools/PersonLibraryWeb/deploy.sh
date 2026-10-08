#!/usr/bin/env bash
#
# 武将库部署脚本（在云服务器上执行），站点地址 http://139.155.98.66/personlib/
#
# 三条约定，改动前请先读：
#   1. nginx 把 /personlib/ 前缀剥掉再转发到本进程的 3001 端口，
#      所以后端代码完全不需要感知子路径（它的 SPA 回退正则、静态目录都按根路径写）。
#   2. 前端产物 dist 里已经内联了 /personlib 前缀（构建时由 .env.production 的
#      VITE_API_PREFIX 注入）。换部署路径必须重新构建，只改 nginx 是不行的。
#   3. 【重要】线上数据是权威。线上编辑器是给玩家用的，部署包里那份的
#      server/data + server/face 只是本地开发数据，绝不能拿来覆盖线上。
#      本脚本会先把线上运行期数据暂存、同步完再原样放回。
#
# 用法：把本目录（含 server/ dist/ package.json）上传到服务器后执行 bash deploy.sh

set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET_DIR="/opt/sango-personlib"
APP_NAME="sango-personlib"
PORT="${PORT:-3001}"

log() { printf '\n[%s] %s\n' "$(date +%H:%M:%S)" "$1"; }

log "1/5 准备目录"
mkdir -p "$TARGET_DIR"

# 首次部署到一台曾经部署过的机器时，线上数据往往在别处的旧目录里。
# 直接跑本脚本会让新目录用部署包里的开发数据启动 —— 先把那份数据搬过来。
if [ ! -f "$TARGET_DIR/server/data/CustomPerson.json" ]; then
  OLD_DIRS=$(ls -d /root/deploy_* /root/*deploy_* 2>/dev/null || true)
  if [ -n "$OLD_DIRS" ]; then
    echo "⚠️  目标目录还没有武将数据，但机器上存在历史部署目录："
    echo "$OLD_DIRS" | sed 's/^/     /'
    echo "    如果线上是活的，请先把那里的 server/data 与 server/face 复制到 $TARGET_DIR，再重跑本脚本。"
    echo "    否则新实例会用部署包里的本地开发数据启动。"
    exit 1
  fi
fi

# server/data 下有两类文件，处理方式完全不同：
#   · 运行期数据（用户产生）：CustomPerson.json / accounts.json / .token-secret /
#     .id-sequence.json —— 以线上为准，见下面的 RUNTIME_PATHS；
#   · 静态数据（随游戏版本走）：PersonLibrary.json（基础武将库）/ options.json（枚举表）/
#     referencePersons.json —— 不属于运行期数据，必须由**部署包自带**。
#
# 第 3 步会 `rm -rf $TARGET_DIR/server` 再整目录拷贝，所以只要部署包里漏了这 3 个文件，
# 线上它们就会被删掉、且没有还原步骤兜底 —— 表现为「基础武将库突然查不到人」。
# 这里提前拦住，而不是等部署完再靠健康检查发现。
STATIC_DATA_FILES=(PersonLibrary.json options.json referencePersons.json)
MISSING_STATIC=()
for f in "${STATIC_DATA_FILES[@]}"; do
  [ -f "$SOURCE_DIR/server/data/$f" ] || MISSING_STATIC+=("$f")
done
if [ ${#MISSING_STATIC[@]} -gt 0 ]; then
  echo "❌ 部署包缺少静态数据文件：${MISSING_STATIC[*]}"
  echo "   它们位于 server/data/，不属于运行期数据，必须随部署包一起上传。"
  echo "   打包时请排除的是运行期文件（CustomPerson.json、accounts.json、"
  echo "   .token-secret、.id-sequence.json）以及 face/、backups/、node_modules/。"
  echo "   继续部署会删除线上对应文件，已中止。"
  exit 1
fi

log "2/5 暂存线上运行期数据（以线上为准）"
STASH="$(mktemp -d)"
RUNTIME_PATHS=(
  "server/data/CustomPerson.json"
  "server/data/accounts.json"
  "server/data/.token-secret"
  "server/data/.id-sequence.json"
  "server/face"
  "server/backups"
)
for rel in "${RUNTIME_PATHS[@]}"; do
  if [ -e "$TARGET_DIR/$rel" ]; then
    mkdir -p "$STASH/$(dirname "$rel")"
    mv "$TARGET_DIR/$rel" "$STASH/$rel"
    echo "  暂存 $rel"
  fi
done

log "3/5 同步代码与前端产物"
rm -rf "$TARGET_DIR/server" "$TARGET_DIR/dist"
cp -r "$SOURCE_DIR/server" "$TARGET_DIR/server"
cp -r "$SOURCE_DIR/dist" "$TARGET_DIR/dist"
cp "$SOURCE_DIR/package.json" "$TARGET_DIR/package.json"

for rel in "${RUNTIME_PATHS[@]}"; do
  if [ -e "$STASH/$rel" ]; then
    mkdir -p "$(dirname "$TARGET_DIR/$rel")"
    rm -rf "$TARGET_DIR/$rel"
    mv "$STASH/$rel" "$TARGET_DIR/$rel"
    echo "  还原 $rel"
  fi
done
rm -rf "$STASH"

log "4/5 安装生产依赖（服务端只用到 express）"
cd "$TARGET_DIR"
npm install --omit=dev --no-audit --no-fund --registry=https://registry.npmmirror.com

log "5/5 启动 / 重载进程"
# MODHUB_BASE_URL 走同机回环：登录与注册都由创意工坊校验身份
if pm2 describe "$APP_NAME" > /dev/null 2>&1; then
  PORT="$PORT" pm2 restart "$APP_NAME" --update-env
else
  PORT="$PORT" pm2 start server/index.js --name "$APP_NAME" --cwd "$TARGET_DIR"
fi
pm2 save > /dev/null

log "部署完成，健康检查"
sleep 3
curl -s -o /dev/null -w '  /api/meta                       -> HTTP %{http_code}\n' "http://127.0.0.1:${PORT}/api/meta" || true
curl -s -o /dev/null -w '  /personlib/                     -> HTTP %{http_code}\n' "http://127.0.0.1/personlib/" || true
curl -s -o /dev/null -w '  /personlib/face/4000_2.png      -> HTTP %{http_code}\n' "http://127.0.0.1/personlib/face/4000_2.png" || true
echo -n '  线上武将数据条数：'
curl -s "http://127.0.0.1/personlib/api/persons?lib=custom" | grep -o '"count":[0-9]*' | head -1
