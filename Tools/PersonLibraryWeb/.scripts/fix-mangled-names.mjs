/**
 * 文件名：fix-mangled-names.mjs
 * 描述：修复被 PowerShell HTTP 请求体编码搞坏的中文字段。
 *
 *       背景：早先用 PowerShell 的 Invoke-WebRequest -Method PUT 改武将字段时，
 *       请求体默认按 ANSI 编码发送，服务端按 UTF-8 解析，导致姓名/字/号/简介
 *       里的中文变成 '?'。字段值本身没有别的变化，所以从备份里按字段补回即可。
 *
 *       用法：
 *         node .scripts/fix-mangled-names.mjs          # 只诊断，不写入
 *         node .scripts/fix-mangled-names.mjs --apply  # 执行修复
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(__dirname, '..');

const TARGET = path.join(ROOT, 'server', 'data', 'CustomPerson.json');
const BACKUP_ROOT = path.join(ROOT, 'server', 'backups');
const APPLY = process.argv.includes('--apply');

/** 可能被写坏的中文字段（其余字段是数字/布尔，不受编码影响） */
const TEXT_FIELDS = ['Name', 'familyName', 'giveName', 'nickName', 'description'];

/** 判断字段值是否被写成乱码：出现 '?' 且原本不该有它 */
function looksMangled(value) {
  return typeof value === 'string' && value.includes('?');
}

function readLibrary(file) {
  return JSON.parse(fs.readFileSync(file, 'utf8')).PersonLibrary;
}

/** 找一个能提供正确值的备份（自动挑最新的一份） */
function findBackup() {
  if (!fs.existsSync(BACKUP_ROOT)) return null;
  const dirs = fs
    .readdirSync(BACKUP_ROOT, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => d.name)
    .sort()
    .reverse();
  for (const name of dirs) {
    const file = path.join(BACKUP_ROOT, name, 'CustomPerson.json');
    if (fs.existsSync(file)) return { name, file };
  }
  return null;
}

const backup = findBackup();
if (!backup) {
  console.error('找不到包含 CustomPerson.json 的备份，无法自动修复');
  process.exit(1);
}
console.log('使用备份：' + backup.name);

const current = readLibrary(TARGET);
const reference = readLibrary(backup.file);

let touched = 0;
const report = [];

for (const key of Object.keys(current)) {
  const person = current[key];
  if (person === null || typeof person !== 'object') continue;

  const source = reference[key];
  if (source === undefined) continue;

  for (const field of TEXT_FIELDS) {
    if (!looksMangled(person[field])) continue;
    if (looksMangled(source[field])) continue; // 备份里也是坏的，跳过免得越修越糟
    report.push(`  [${key}] ${field}: ${JSON.stringify(person[field])}  ->  ${JSON.stringify(source[field])}`);
    person[field] = source[field];
    touched += 1;
  }
}

if (report.length === 0) {
  console.log('没有发现被写坏的中文字段，无需修复。');
  process.exit(0);
}

console.log(`待修复字段 ${report.length} 处（涉及 ${new Set(report.map((r) => r.trim().split(' ')[0])).size} 位武将）：`);
for (const line of report) console.log(line);

if (!APPLY) {
  console.log('\n这是诊断模式，未写入。确认无误后加 --apply 执行修复。');
  process.exit(0);
}

fs.writeFileSync(TARGET, JSON.stringify({ PersonLibrary: current }, null, 2) + '\n', 'utf8');
console.log(`\n已修复 ${touched} 个字段并写入 ${TARGET}`);
