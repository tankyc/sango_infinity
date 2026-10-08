/**
 * Sango Infinity —— 特技效果编辑器 主逻辑
 * 依赖 schema.js（window.SCHEMA）
 */
(function () {
    'use strict';

    var S = window.SCHEMA;
    var LS_KEY = 'sango_effect_editor_v1';
    var seq = 0;

    var State = {
        host: 'feature',
        root: [],        // 根节点数组
        sel: null,       // 当前选中节点 id
        palTab: 'action',// 节点库当前标签页
        collapsed: {}    // 折叠状态
    };

    var KIND_ORDER = ['action', 'trigger', 'condition'];
    var KIND_LABEL = { action: '效果 Action', trigger: '触发器 Trigger', condition: '条件 Condition' };
    var GROUP_ORDER = ['组合', '部队', '城市', '建筑', '势力', '逻辑', '通用', '战法', '技能', '武将', '地图'];

    /* ============================================================
     * 工具
     * ========================================================== */
    function uid() { return 'n' + (++seq) + '_' + Math.random().toString(36).slice(2, 6); }
    function esc(s) {
        return String(s === undefined || s === null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }
    function defOf(kind, cls) { return (S.kinds[kind] || {})[cls] || null; }
    function clone(o) { return JSON.parse(JSON.stringify(o)); }
    function hostDef() {
        for (var i = 0; i < S.hosts.length; i++) if (S.hosts[i].id === State.host) return S.hosts[i];
        return S.hosts[0];
    }
    function hostFieldName() {
        var p = hostDef().path || '';
        var seg = p.split('.');
        return seg[seg.length - 1] || 'actionEntities';
    }

    /* ============================================================
     * 节点模型
     * ========================================================== */
    function createNode(kind, cls) {
        var def = defOf(kind, cls);
        var n = { id: uid(), kind: kind, cls: cls, fields: {}, slots: {} };
        if (!def) { n.unknown = true; n.raw = { class: cls }; return n; }
        (def.fields || []).forEach(function (f) { n.fields[f.name] = f.def; });
        (def.slots || []).forEach(function (s) { n.slots[s.name] = []; });
        return n;
    }

    /** 解析已有 JSON 为节点（未知 class 保留原始 JSON，不丢数据） */
    function parseNode(obj, kind) {
        if (!obj || typeof obj !== 'object') return null;
        var cls = obj['class'];
        var reg = S.kinds[kind] || {};
        var realCls = cls;
        if (!reg[cls]) {
            for (var k in reg) {
                if (reg[k].aliases && reg[k].aliases.indexOf(cls) >= 0) { realCls = k; break; }
            }
        }
        if (!reg[realCls]) {
            return { id: uid(), kind: kind, cls: cls, unknown: true, raw: clone(obj), fields: {}, slots: {} };
        }
        var def = reg[realCls];
        var n = { id: uid(), kind: kind, cls: realCls, fields: {}, slots: {} };
        (def.fields || []).forEach(function (f) {
            var v = obj[f.name];
            if (v !== undefined && v !== null) n.fields[f.name] = v;
            else n.fields[f.name] = f.def;
        });
        (def.slots || []).forEach(function (s) {
            var raw = obj[s.name];
            if (s.kind === 'single') {
                n.slots[s.name] = raw ? [parseNode(raw, s.accept)].filter(Boolean) : [];
            } else {
                n.slots[s.name] = Array.isArray(raw) ? raw.map(function (x) { return parseNode(x, s.accept); }).filter(Boolean) : [];
            }
        });
        return n;
    }

    /** 查找节点：返回 { node, parent, slotName, index }，parent 为 null 表示根 */
    function locate(id, list, parent, slotName) {
        for (var i = 0; i < list.length; i++) {
            var n = list[i];
            if (n.id === id) return { node: n, parent: parent, slotName: slotName, index: i, list: list };
            var def = defOf(n.kind, n.cls);
            if (def && def.slots) {
                for (var s = 0; s < def.slots.length; s++) {
                    var sn = def.slots[s].name;
                    var r = locate(id, n.slots[sn] || [], n, sn);
                    if (r) return r;
                }
            }
        }
        return null;
    }
    function findNode(id) { return locate(id, State.root, null, null); }
    function getList(parent, slotName) { return parent ? (parent.slots[slotName] || []) : State.root; }
    function setList(parent, slotName, arr) {
        if (parent) parent.slots[slotName] = arr; else State.root = arr;
    }

    /** 判断 from 是否为 target 的祖先（防止把父节点拖进自己的子树） */
    function isAncestor(ancestorId, node) {
        if (!node) return false;
        var def = defOf(node.kind, node.cls);
        if (!def || !def.slots) return false;
        for (var i = 0; i < def.slots.length; i++) {
            var arr = node.slots[def.slots[i].name] || [];
            for (var j = 0; j < arr.length; j++) {
                if (arr[j].id === ancestorId) return true;
                if (isAncestor(ancestorId, arr[j])) return true;
            }
        }
        return false;
    }

    /* ============================================================
     * 序列化
     * ========================================================== */
    function shouldEmit(f, v) {
        if (f.required || f.force) return true;
        if (f.type === 'enum[]' || f.type === 'int[]') return !!(v && v.length);
        if (f.type === 'enum' || f.type === 'bool') return v !== f.def;
        if (f.optional) return !(v === 0 || v === '' || v === null || v === undefined);
        return true;
    }
    function serializeNode(n) {
        if (n.unknown) return clone(n.raw || { class: n.cls });
        var def = defOf(n.kind, n.cls);
        if (!def) return { class: n.cls };
        var o = { class: n.cls };
        (def.fields || []).forEach(function (f) {
            var v = n.fields[f.name];
            if (v === undefined || v === null) v = f.def;
            if (shouldEmit(f, v)) o[f.name] = v;
        });
        (def.slots || []).forEach(function (s) {
            var arr = n.slots[s.name] || [];
            if (s.kind === 'single') {
                if (arr.length) o[s.name] = serializeNode(arr[0]);
                else if (s.force) o[s.name] = null;
            } else {
                if (arr.length || s.force) o[s.name] = arr.map(serializeNode);
            }
        });
        return o;
    }
    function serializeRoot() { return State.root.map(serializeNode); }

    /* ============================================================
     * 校验
     * ========================================================== */
    function validate() {
        var issues = [];
        function add(level, nodeId, text) { issues.push({ level: level, id: nodeId, text: text }); }

        State.root.forEach(function (n) { walk(n); });
        function walk(n) {
            if (n.unknown) {
                var why = S.blacklist[n.cls];
                add('err', n.id, '未注册的 class「' + n.cls + '」' + (why ? '（' + why + '）' : '，节点库中没有该类型'));
                return;
            }
            var def = defOf(n.kind, n.cls);
            if (!def) { add('err', n.id, '未注册的 class「' + n.cls + '」'); return; }
            if (def.warn) add('warn', n.id, def.warn);

            (def.fields || []).forEach(function (f) {
                if (!f.required) return;
                var v = n.fields[f.name];
                var empty = (v === undefined || v === null || v === '' || (Array.isArray(v) && !v.length));
                if (empty) add('err', n.id, '必填项「' + f.label + '(' + f.name + ')」未填写');
            });
            // 候选值与权重数量必须一致
            if (n.fields.values && n.fields.weight && n.fields.values.length !== n.fields.weight.length) {
                add('err', n.id, 'values(' + n.fields.values.length + ') 与 weight(' + n.fields.weight.length + ') 数量不一致');
            }
            if (def.slots) {
                def.slots.forEach(function (s) {
                    var arr = n.slots[s.name] || [];
                    if (s.kind === 'array' && !arr.length) {
                        add('warn', n.id, '「' + s.label + '(' + s.name + ')」为空，该效果不会产生任何作用');
                    }
                    arr.forEach(walk);
                });
            }
        }
        return issues;
    }

    /* ============================================================
     * 渲染：节点库
     * ========================================================== */
    /** 节点是否命中搜索关键词 */
    function matchNode(q, d, cls) {
        if (!q) return true;
        return (d.label + ' ' + cls + ' ' + (d.desc || '')).toLowerCase().indexOf(q) >= 0;
    }

    /** 统计某一类节点在当前搜索词下的命中数量 */
    function countKind(kind, q) {
        var reg = S.kinds[kind];
        return Object.keys(reg).filter(function (cls) { return matchNode(q, reg[cls], cls); }).length;
    }

    /** 渲染某一类节点的分组列表 */
    function renderKind(kind, q) {
        var reg = S.kinds[kind];
        var groups = {};
        Object.keys(reg).forEach(function (cls) {
            var d = reg[cls];
            var g = d.group || '其它';
            (groups[g] = groups[g] || []).push(cls);
        });
        var keys = Object.keys(groups).sort(function (a, b) {
            var ia = GROUP_ORDER.indexOf(a), ib = GROUP_ORDER.indexOf(b);
            return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib);
        });
        var html = '';
        keys.forEach(function (g) {
            var items = groups[g].filter(function (cls) { return matchNode(q, reg[cls], cls); });
            if (!items.length) return;
            html += '<div class="pgroup"><div class="pgroup-head">' + esc(g) +
                '<span class="cnt">' + items.length + '</span></div><div class="pgroup-items">';
            items.forEach(function (cls) {
                var d = reg[cls];
                html += '<div class="pitem k-' + kind + '" draggable="true" data-kind="' + kind + '" data-cls="' + esc(cls) + '">' +
                    '<div class="t">' + esc(d.label) + '</div>' +
                    '<div class="c">' + esc(cls) + '</div>' +
                    (d.desc ? '<div class="d">' + esc(d.desc) + '</div>' : '') +
                    '<button class="padd" data-add="1" data-kind="' + kind + '" data-cls="' + esc(cls) + '" title="添加到效果树">＋</button>' +
                    '</div>';
            });
            html += '</div></div>';
        });
        return html;
    }

    /** 刷新标签页：当前高亮 + 数量角标；搜索时其它标签页有命中会高亮提示 */
    function renderPaletteTabs(counts, searching) {
        Array.prototype.forEach.call(document.querySelectorAll('#palTabs .ptab'), function (btn) {
            var kind = btn.getAttribute('data-tab');
            var cnt = btn.querySelector('.cnt');
            cnt.textContent = counts[kind] || 0;
            btn.classList.toggle('active', kind === State.palTab);
            btn.classList.toggle('has-hit', !!(searching && kind !== State.palTab && counts[kind]));
        });
    }

    function renderPalette() {
        var q = (document.getElementById('paletteSearch').value || '').trim().toLowerCase();
        var counts = {};
        KIND_ORDER.forEach(function (kind) { counts[kind] = countKind(kind, q); });
        renderPaletteTabs(counts, !!q);
        var html = renderKind(State.palTab, q);
        if (!html) {
            var other = KIND_ORDER.some(function (k) { return k !== State.palTab && counts[k]; });
            html = '<div class="empty-tip">没有匹配的节点' + (other ? '，其它标签页有结果' : '') + '</div>';
        }
        document.getElementById('paletteBody').innerHTML = html;
    }

    /* ============================================================
     * 渲染：画布
     * ========================================================== */
    function nodeSummary(n) {
        var def = defOf(n.kind, n.cls);
        if (!def) return '';
        var parts = [];
        (def.fields || []).forEach(function (f) {
            var v = n.fields[f.name];
            if (f.type === 'enum[]' || f.type === 'int[]') {
                if (v && v.length) parts.push(f.label + ':' + v.join(','));
                return;
            }
            if (f.type === 'bool') { if (v !== f.def) parts.push(f.label + (v ? '✔' : '✘')); return; }
            if (v === f.def || v === 0 || v === '') return;
            var label = v;
            if (f.type === 'enum') {
                var list = S.enums[f.enum] || [];
                for (var i = 0; i < list.length; i++) if (list[i].v === v) { label = String(list[i].l).split(' ')[0]; break; }
            }
            parts.push(f.label + ':' + label);
        });
        return parts.slice(0, 4).join(' · ');
    }

    /** 生成放置区（gap） */
    function dz(slotKey, index) {
        return '<div class="dropzone" data-drop="1" data-slot="' + esc(slotKey) + '" data-index="' + index + '"></div>';
    }

    function renderSlotItems(list, slotKey, accept) {
        var html = '';
        for (var i = 0; i < list.length; i++) {
            html += dz(slotKey, i) + renderNode(list[i]);
        }
        html += dz(slotKey, list.length);
        return html;
    }

    function renderNode(n) {
        var def = defOf(n.kind, n.cls);
        var title = def ? def.label : ('未知：' + n.cls);
        var cls = n.cls;
        var collapsed = !!State.collapsed[n.id];
        var err = n.unknown ? ' has-error' : '';
        var html = '<div class="node k-' + n.kind + (State.sel === n.id ? ' selected' : '') + err + (n.unknown ? ' unknown' : '') +
            (collapsed ? ' collapsed' : '') + '" data-node="' + n.id + '">';
        html += '<div class="node-head" data-select="' + n.id + '">' +
            '<span class="node-grip" draggable="true" data-drag="' + n.id + '" title="拖动以移动">⋮⋮</span>' +
            '<span class="icon-btn" data-toggle="' + n.id + '">' + (collapsed ? '▸' : '▾') + '</span>' +
            '<span class="node-title">' + esc(title) + '</span>' +
            '<span class="node-cls">' + esc(cls) + '</span>' +
            '<span class="node-sum">' + esc(nodeSummary(n)) + '</span>' +
            '<span class="node-ops">' +
            '<button class="icon-btn" data-dup="' + n.id + '" title="复制">⧉</button>' +
            '<button class="icon-btn danger" data-del="' + n.id + '" title="删除">✕</button>' +
            '</span></div>';

        if (!collapsed) {
            html += '<div class="node-body">';
            if (n.unknown) {
                html += '<div class="hint">该 class 未在节点库中登记，序列化时将原样输出原始数据。</div>';
            }
            if (def && def.slots && def.slots.length) {
                def.slots.forEach(function (s) {
                    var arr = n.slots[s.name] || [];
                    html += '<div class="slot-box"><div class="slot-box-head">' + esc(s.label) +
                        ' <span style="opacity:.6">' + esc(s.name) + '</span>' +
                        (s.force ? ' <span style="color:var(--warn)">必输出</span>' : '') + '</div>';
                    html += '<div class="slot-drop" data-drop="1" data-slot="' + n.id + '::' + esc(s.name) +
                        '" data-accept="' + s.accept + '" data-kind="' + (s.kind === 'single' ? 'single' : 'array') + '">' +
                        '<div class="slot-items">' + renderSlotItems(arr, n.id + '::' + s.name, s.accept) + '</div>' +
                        (arr.length ? '' : '<div class="slot-empty">拖入「' + KIND_LABEL[s.accept].split(' ')[0] + '」</div>') +
                        '</div></div>';
                });
            }
            html += '</div>';
        }
        html += '</div>';
        return html;
    }

    function renderCanvas() {
        var box = document.getElementById('rootItems');
        box.innerHTML = renderSlotItems(State.root, '__root__', hostDef().root || 'action');
        document.getElementById('rootLabel').textContent = hostFieldName();
        var empty = document.querySelector('.root-slot .slot-empty');
        if (empty) {
            empty.textContent = '把「效果 Action」节点拖到这里';
            empty.style.display = State.root.length ? 'none' : '';
        }
    }

    /* ============================================================
     * 渲染：属性面板
     * ========================================================== */
    function renderInspector() {
        var box = document.getElementById('inspectorBody');
        var found = State.sel ? findNode(State.sel) : null;
        if (!found) { box.innerHTML = '<div class="empty-tip">在画布中选择一个节点</div>'; return; }
        var n = found.node;
        var def = defOf(n.kind, n.cls);
        var html = '<div class="insp-head"><div class="t">' + esc(def ? def.label : '未知节点') +
            '</div><div class="c">' + esc(n.cls) + '</div>';
        if (def && def.desc) html += '<div class="d">' + esc(def.desc) + '</div>';
        if (def && def.warn) html += '<div class="insp-warn">' + esc(def.warn) + '</div>';
        if (n.unknown) html += '<div class="insp-warn">该 class 未登记，属性不可编辑，导出时原样保留原始 JSON。</div>';
        html += '</div>';

        if (def && !n.unknown) {
            (def.fields || []).forEach(function (f) {
                var v = n.fields[f.name];
                if (v === undefined) v = f.def;
                html += '<div class="frow"><label>' + esc(f.label) +
                    (f.required ? ' <span class="req">*</span>' : '') +
                    (f.unit ? ' <span class="unit">(' + (f.unit === 'percent' ? '百分比' : '万分比') + ')</span>' : '') +
                    '</label>' + renderFieldControl(n, f, v) +
                    (f.tip ? '<div class="tip">' + esc(f.tip) + '</div>' : '') + '</div>';
            });
            if (!(def.fields || []).length) html += '<div class="hint">该节点没有参数。</div>';
            if (def.slots && def.slots.length) {
                html += '<div class="frow"><label>子节点</label><div class="tip">在画布中直接拖入 / 拖动排序。</div></div>';
            }
        } else if (n.unknown) {
            html += '<pre style="white-space:pre-wrap;font-size:11px;color:#9aa5b5">' + esc(JSON.stringify(n.raw, null, 2)) + '</pre>';
        }
        box.innerHTML = html;
    }

    function renderFieldControl(n, f, v) {
        var id = 'f_' + n.id + '_' + f.name;
        if (f.type === 'bool') {
            return '<label class="fswitch"><input type="checkbox" data-fid="' + n.id + '" data-fname="' + f.name +
                '" data-ftype="bool"' + (v ? ' checked' : '') + '> ' + (v ? 'true' : 'false') + '</label>';
        }
        if (f.type === 'enum') {
            var opts = S.enums[f.enum] || [];
            var s = '<select data-fid="' + n.id + '" data-fname="' + f.name + '" data-ftype="enum">';
            opts.forEach(function (o) {
                s += '<option value="' + esc(o.v) + '"' + (String(o.v) === String(v) ? ' selected' : '') + '>' + esc(o.l) + '</option>';
            });
            return s + '</select>';
        }
        if (f.type === 'enum[]') {
            var list = S.enums[f.enum] || [];
            var arr = Array.isArray(v) ? v : [];
            var h = '<div class="chips" data-fid="' + n.id + '" data-fname="' + f.name + '" data-ftype="enum[]">';
            list.forEach(function (o) {
                h += '<span class="chip' + (arr.indexOf(o.v) >= 0 ? ' on' : '') + '" data-val="' + esc(o.v) + '">' + esc(o.l) + '</span>';
            });
            return h + '</div>';
        }
        if (f.type === 'int[]') {
            var val = Array.isArray(v) ? v.join(', ') : '';
            return '<input type="text" data-fid="' + n.id + '" data-fname="' + f.name +
                '" data-ftype="int[]" value="' + esc(val) + '" placeholder="例：1, 2">';
        }
        return '<input type="number" data-fid="' + n.id + '" data-fname="' + f.name +
            '" data-ftype="int" value="' + esc(v === undefined || v === null ? 0 : v) + '">';
    }

    /* ============================================================
     * 渲染：JSON 与问题列表
     * ========================================================== */
    function renderJson() {
        var arr = serializeRoot();
        document.getElementById('jsonOut').textContent = JSON.stringify(arr, null, 2);
        var issues = validate();
        var ul = document.getElementById('issueList');
        var errCount = issues.filter(function (i) { return i.level === 'err'; }).length;
        var warnCount = issues.filter(function (i) { return i.level === 'warn'; }).length;
        var badge = document.getElementById('validateBadge');
        badge.className = 'badge ' + (errCount ? 'err' : (warnCount ? 'warn' : 'ok'));
        badge.textContent = errCount ? (errCount + ' 个错误') : (warnCount ? (warnCount + ' 个警告') : '正常');
        if (!issues.length) {
            ul.innerHTML = '<li class="ok">校验通过，可以复制</li>';
            return;
        }
        ul.innerHTML = issues.map(function (i) {
            return '<li class="' + i.level + '" data-goto="' + i.id + '">' +
                (i.level === 'err' ? '✕ ' : '! ') + esc(i.text) + '</li>';
        }).join('');
    }

    function renderAll() { renderCanvas(); renderInspector(); renderJson(); save(); }

    /* ============================================================
     * 本地存储
     * ========================================================== */
    var saveTimer = null;
    function save() {
        clearTimeout(saveTimer);
        saveTimer = setTimeout(function () {
            try { localStorage.setItem(LS_KEY, JSON.stringify({ host: State.host, root: State.root })); } catch (e) { }
        }, 300);
    }
    function load() {
        try {
            var raw = localStorage.getItem(LS_KEY);
            if (!raw) return false;
            var d = JSON.parse(raw);
            if (!d || !Array.isArray(d.root)) return false;
            State.host = d.host || 'feature';
            State.root = d.root;
            return true;
        } catch (e) { return false; }
    }

    /* ============================================================
     * 拖拽
     * ========================================================== */
    var dragPayload = null;

    document.addEventListener('dragstart', function (e) {
        var item = e.target.closest ? e.target.closest('[data-cls]') : null;
        if (item && item.classList.contains('pitem')) {
            dragPayload = { src: 'palette', kind: item.getAttribute('data-kind'), cls: item.getAttribute('data-cls') };
            e.dataTransfer.effectAllowed = 'copy';
            try { e.dataTransfer.setData('text/plain', JSON.stringify(dragPayload)); } catch (err) { }
            return;
        }
        var grip = e.target.closest ? e.target.closest('[data-drag]') : null;
        if (grip) {
            var id = grip.getAttribute('data-drag');
            var f = findNode(id);
            if (!f) return;
            dragPayload = { src: 'tree', id: id, kind: f.node.kind };
            e.dataTransfer.effectAllowed = 'move';
            try { e.dataTransfer.setData('text/plain', JSON.stringify(dragPayload)); } catch (err) { }
        }
    });
    /** 清除放置高亮 */
    function clearZone(el) {
        if (el) el.classList.remove('over', 'drag-ok', 'drag-no', 'drop-candidate');
    }
    function clearAllZones() {
        Array.prototype.forEach.call(
            document.querySelectorAll('.over,.drag-ok,.drag-no,.drop-candidate'), clearZone);
    }
    document.addEventListener('dragend', function () {
        dragPayload = null;
        clearAllZones();
    });

    function slotDef(slotKey) {
        if (slotKey === '__root__') return null;
        var parts = slotKey.split('::');
        var f = findNode(parts[0]);
        if (!f) return null;
        var def = defOf(f.node.kind, f.node.cls);
        if (!def || !def.slots) return null;
        for (var i = 0; i < def.slots.length; i++) if (def.slots[i].name === parts[1]) return def.slots[i];
        return null;
    }
    function slotAccept(slotKey) {
        if (slotKey === '__root__') return hostDef().root || 'action';
        var s = slotDef(slotKey);
        return s ? s.accept : null;
    }
    function slotIsSingle(slotKey) {
        var s = slotDef(slotKey);
        return !!(s && s.kind === 'single');
    }

    /** 取得某个放置键对应的父节点与槽位名 */
    function ownerOf(slotKey) {
        if (slotKey === '__root__') return { parent: null, slotName: null };
        var f = findNode(slotKey.split('::')[0]);
        return { parent: f ? f.node : null, slotName: slotKey.split('::')[1] };
    }

    function canDrop(slotKey) {
        if (!dragPayload) return false;
        var accept = slotAccept(slotKey);
        if (!accept || dragPayload.kind !== accept) return false;
        var own = ownerOf(slotKey);
        if (dragPayload.src === 'tree') {
            if (isAncestor(dragPayload.id, own.parent)) return false;
        } else if (slotIsSingle(slotKey)) {
            var cur = getList(own.parent, own.slotName);
            if (cur && cur.length) return false; // 单值槽位已有内容
        }
        return true;
    }

    document.addEventListener('dragover', function (e) {
        var zone = e.target.closest ? e.target.closest('[data-drop]') : null;
        if (!zone || !dragPayload) return;
        var slotKey = zone.getAttribute('data-slot');
        var ok = canDrop(slotKey);
        e.preventDefault();
        e.dataTransfer.dropEffect = dragPayload.src === 'tree' ? 'move' : 'copy';
        if (zone.id === 'canvasBody') {
            // 画布空白区：根容器的兜底放置区，始终追加到末尾，仅用轻微底色提示
            zone.classList.toggle('drop-candidate', ok);
        } else if (zone.classList.contains('dropzone')) {
            zone.classList.toggle('over', ok);
        } else {
            zone.classList.toggle('drag-ok', ok);
            zone.classList.toggle('drag-no', !ok);
        }
    });
    document.addEventListener('dragleave', function (e) {
        var zone = e.target.closest ? e.target.closest('[data-drop]') : null;
        if (!zone) return;
        clearZone(zone);
    });
    document.addEventListener('drop', function (e) {
        var zone = e.target.closest ? e.target.closest('[data-drop]') : null;
        if (!zone) return;
        e.preventDefault();
        clearAllZones();
        var slotKey = zone.getAttribute('data-slot');
        var index = parseInt(zone.getAttribute('data-index') || '-1', 10);
        doDrop(slotKey, index);
    });

    /* ============================================================
     * 点击添加（触屏不支持 HTML5 拖拽，用「＋」作为等价入口）
     * ========================================================== */
    /** 计算落点：优先当前选中节点里匹配的槽位（single 空槽 > array 槽），其次根容器 */
    function resolveAddTarget(kind) {
        if (State.sel) {
            var f = findNode(State.sel);
            if (f && !f.node.unknown) {
                var def = defOf(f.node.kind, f.node.cls);
                var slots = (def && def.slots) || [];
                var arrSlot = null;
                for (var i = 0; i < slots.length; i++) {
                    var s = slots[i];
                    if (s.accept !== kind) continue;
                    var arr = f.node.slots[s.name] || [];
                    if (s.kind === 'single') {
                        if (!arr.length) return { parent: f.node, slotName: s.name };
                    } else if (!arrSlot) {
                        arrSlot = { parent: f.node, slotName: s.name };
                    }
                }
                if (arrSlot) return arrSlot;
            }
        }
        if ((hostDef().root || 'action') === kind) return { parent: null, slotName: null };
        return null;
    }

    /** 节点库「＋」：把节点加入效果树 */
    function addNodeByClick(kind, cls) {
        var t = resolveAddTarget(kind);
        if (!t) {
            toast('当前位置不接受「' + KIND_LABEL[kind] + '」节点');
            return;
        }
        var n = createNode(kind, cls);
        var list = getList(t.parent, t.slotName);
        list.push(n);
        setList(t.parent, t.slotName, list);
        State.sel = n.id;
        renderAll();
        var d = defOf(kind, cls);
        toast('已添加：' + (d ? d.label : cls));
        // 窄屏下添加完直接跳到编辑区，方便继续操作
        if (isNarrow()) setMobileView('canvas');
    }

    /* ============================================================
     * 窄屏视图切换
     * ========================================================== */
    function isNarrow() {
        return !!(window.matchMedia && window.matchMedia('(max-width: 900px)').matches);
    }
    function setMobileView(view) {
        document.body.classList.remove('m-palette', 'm-canvas', 'm-inspector');
        document.body.classList.add('m-' + view);
        Array.prototype.forEach.call(document.querySelectorAll('#mTabs .mtab'), function (b) {
            b.classList.toggle('active', b.getAttribute('data-view') === view);
        });
    }

    function doDrop(slotKey, index) {
        if (!dragPayload || !canDrop(slotKey)) { dragPayload = null; return; }
        var own = ownerOf(slotKey);
        var parentNode = own.parent, slotName = own.slotName;
        var list = getList(parentNode, slotName);
        if (index < 0) index = list.length;

        if (dragPayload.src === 'palette') {
            var n = createNode(dragPayload.kind, dragPayload.cls);
            if (slotIsSingle(slotKey) && list.length) list = [n];
            else list.splice(index, 0, n);
            setList(parentNode, slotName, list);
            State.sel = n.id;
        } else {
            var f = findNode(dragPayload.id);
            if (!f) return;
            // 先从原位置移除
            var oldList = getList(f.parent, f.slotName);
            oldList.splice(f.index, 1);
            setList(f.parent, f.slotName, oldList);
            // 同一列表内前移时修正下标
            if (f.parent === parentNode && f.slotName === slotName && f.index < index) index--;
            var nl = getList(parentNode, slotName);
            if (slotIsSingle(slotKey) && nl.length) nl = [f.node];
            else nl.splice(index, 0, f.node);
            setList(parentNode, slotName, nl);
            State.sel = f.node.id;
        }
        dragPayload = null;
        renderAll();
    }

    /* ============================================================
     * 事件绑定
     * ========================================================== */
    function bind() {
        // 节点库搜索
        document.getElementById('paletteSearch').addEventListener('input', renderPalette);
        // 节点库标签页切换
        document.getElementById('palTabs').addEventListener('click', function (e) {
            var tab = e.target.closest ? e.target.closest('[data-tab]') : null;
            if (!tab) return;
            State.palTab = tab.getAttribute('data-tab');
            renderPalette();
        });
        // 窄屏面板切换
        document.getElementById('mTabs').addEventListener('click', function (e) {
            var b = e.target.closest ? e.target.closest('[data-view]') : null;
            if (b) setMobileView(b.getAttribute('data-view'));
        });
        // 分组折叠 / 「＋」添加
        document.getElementById('paletteBody').addEventListener('click', function (e) {
            var add = e.target.closest('[data-add]');
            if (add) {
                addNodeByClick(add.getAttribute('data-kind'), add.getAttribute('data-cls'));
                return;
            }
            var h = e.target.closest('.pgroup-head');
            if (!h) return;
            var g = h.parentNode;
            if (g.classList.contains('pgroup')) g.classList.toggle('collapsed');
        });

        // 画布点击
        document.getElementById('canvasBody').addEventListener('click', function (e) {
            var t = e.target;
            var sel = t.closest('[data-select]');
            if (sel && !t.closest('[data-toggle]') && !t.closest('[data-del]') && !t.closest('[data-dup]') && !t.closest('[data-drag]')) {
                State.sel = sel.getAttribute('data-select');
                renderCanvas(); renderInspector();
                return;
            }
            var tg = t.closest('[data-toggle]');
            if (tg) {
                var id1 = tg.getAttribute('data-toggle');
                State.collapsed[id1] = !State.collapsed[id1];
                renderCanvas();
                return;
            }
            var del = t.closest('[data-del]');
            if (del) {
                var id2 = del.getAttribute('data-del');
                var f2 = findNode(id2);
                if (f2) {
                    var l2 = getList(f2.parent, f2.slotName);
                    l2.splice(f2.index, 1);
                    setList(f2.parent, f2.slotName, l2);
                    if (State.sel === id2) State.sel = null;
                }
                renderAll();
                return;
            }
            var dup = t.closest('[data-dup]');
            if (dup) {
                var id3 = dup.getAttribute('data-dup');
                var f3 = findNode(id3);
                if (f3) {
                    var copy = cloneNode(f3.node);
                    var l3 = getList(f3.parent, f3.slotName);
                    l3.splice(f3.index + 1, 0, copy);
                    setList(f3.parent, f3.slotName, l3);
                    State.sel = copy.id;
                }
                renderAll();
            }
        });

        // 属性面板编辑
        var insp = document.getElementById('inspectorBody');
        insp.addEventListener('input', function (e) {
            var el = e.target;
            if (!el.getAttribute || !el.getAttribute('data-fid')) return;
            applyField(el, true);
        });
        insp.addEventListener('change', function (e) {
            var el = e.target;
            if (!el.getAttribute || !el.getAttribute('data-fid')) return;
            applyField(el, false);
            renderInspector();
        });
        insp.addEventListener('click', function (e) {
            var chip = e.target.closest ? e.target.closest('.chip') : null;
            if (!chip) return;
            var box = chip.parentNode;
            var node = findNode(box.getAttribute('data-fid'));
            if (!node) return;
            var fname = box.getAttribute('data-fname');
            var val = parseInt(chip.getAttribute('data-val'), 10);
            var arr = node.node.fields[fname] || [];
            var i = arr.indexOf(val);
            if (i >= 0) arr.splice(i, 1); else arr.push(val);
            arr.sort(function (a, b) { return a - b; });
            node.node.fields[fname] = arr;
            chip.classList.toggle('on');
            renderCanvas(); renderJson(); save();
        });

        // 问题定位
        document.getElementById('issueList').addEventListener('click', function (e) {
            var li = e.target.closest('[data-goto]');
            if (!li) return;
            State.sel = li.getAttribute('data-goto');
            renderCanvas(); renderInspector();
            var el = document.querySelector('[data-node="' + State.sel + '"]');
            if (el) el.scrollIntoView({ block: 'center' });
        });

        // 顶部
        var hostSel = document.getElementById('hostSel');
        S.hosts.forEach(function (h) {
            var o = document.createElement('option');
            o.value = h.id; o.textContent = h.label;
            hostSel.appendChild(o);
        });
        hostSel.value = State.host;
        hostSel.addEventListener('change', function () {
            State.host = hostSel.value;
            updatePathHint();
            renderAll();
        });

        document.getElementById('btnClear').addEventListener('click', function () {
            if (!State.root.length || confirm('确定清空整个效果树？')) {
                State.root = []; State.sel = null; renderAll();
            }
        });
        document.getElementById('btnExpandAll').addEventListener('click', function () { State.collapsed = {}; renderCanvas(); });
        document.getElementById('btnCollapseAll').addEventListener('click', function () {
            State.collapsed = {};
            walkAll(State.root, function (n) { State.collapsed[n.id] = true; });
            renderCanvas();
        });
        document.getElementById('btnToggleJson').addEventListener('click', function () {
            var b = document.querySelector('.jsonbar');
            b.classList.toggle('collapsed');
            this.textContent = b.classList.contains('collapsed') ? '展开' : '收起';
        });

        document.getElementById('btnCopyArray').addEventListener('click', function () {
            copyText(JSON.stringify(serializeRoot(), null, 2), '已复制效果数组');
        });
        document.getElementById('btnCopyWrapped').addEventListener('click', function () {
            var o = {}; o[hostFieldName()] = serializeRoot();
            copyText(JSON.stringify(o, null, 2), '已复制（带字段名 ' + hostFieldName() + '）');
        });
        document.getElementById('btnDownload').addEventListener('click', function () {
            var o = {}; o[hostFieldName()] = serializeRoot();
            download(hostFieldName() + '.json', JSON.stringify(o, null, 2));
        });

        document.getElementById('btnExport').addEventListener('click', function () {
            download('effect_project.json', JSON.stringify({ version: 1, host: State.host, root: State.root }, null, 2));
        });
        document.getElementById('btnLoadProj').addEventListener('click', function () {
            document.getElementById('projFile').click();
        });
        document.getElementById('projFile').addEventListener('change', function (e) {
            var file = e.target.files[0];
            if (!file) return;
            var fr = new FileReader();
            fr.onload = function () {
                try {
                    var d = JSON.parse(fr.result);
                    if (!Array.isArray(d.root)) throw new Error('不是有效的工程文件');
                    State.root = d.root; State.host = d.host || State.host; State.sel = null;
                    document.getElementById('hostSel').value = State.host;
                    updatePathHint(); renderAll();
                } catch (err) { alert('载入失败：' + err.message); }
            };
            fr.readAsText(file);
            e.target.value = '';
        });

        // 模态
        document.getElementById('btnImport').addEventListener('click', openImport);
        document.getElementById('btnHelp').addEventListener('click', function () {
            document.getElementById('helpModal').hidden = false;
        });
        document.getElementById('btnDoImport').addEventListener('click', doImport);
        Array.prototype.forEach.call(document.querySelectorAll('[data-close]'), function (b) {
            b.addEventListener('click', function () {
                var m = b.closest('.modal');
                if (m) m.hidden = true;
            });
        });
        Array.prototype.forEach.call(document.querySelectorAll('.modal'), function (m) {
            m.addEventListener('click', function (e) { if (e.target === m) m.hidden = true; });
        });
    }

    /** 深拷贝节点树，并为所有子节点重新分配 id */
    function cloneNode(n) {
        var c = clone(n);
        c.id = uid();
        (function reid(x) {
            Object.keys(x.slots || {}).forEach(function (k) {
                (x.slots[k] || []).forEach(function (ch) { ch.id = uid(); reid(ch); });
            });
        })(c);
        return c;
    }

    function walkAll(list, cb) {
        list.forEach(function (n) {
            cb(n);
            var def = defOf(n.kind, n.cls);
            if (def && def.slots) def.slots.forEach(function (s) { walkAll(n.slots[s.name] || [], cb); });
        });
    }

    function applyField(el, light) {
        var node = findNode(el.getAttribute('data-fid'));
        if (!node) return;
        var fname = el.getAttribute('data-fname');
        var ftype = el.getAttribute('data-ftype');
        var v;
        if (ftype === 'bool') v = el.checked;
        else if (ftype === 'int') v = (el.value === '' ? 0 : parseInt(el.value, 10));
        else if (ftype === 'int[]') {
            v = el.value.split(/[,，\s]+/).filter(function (x) { return x !== ''; }).map(function (x) { return parseInt(x, 10); });
            v = v.filter(function (x) { return !isNaN(x); });
        } else if (ftype === 'enum') {
            var raw = el.value;
            v = /^-?\d+$/.test(raw) ? parseInt(raw, 10) : raw;
        } else return;
        node.node.fields[fname] = v;
        renderCanvas(); renderJson(); save();
    }

    function updatePathHint() {
        document.getElementById('pathHint').textContent = hostDef().path || '';
    }

    function copyText(text, okMsg) {
        function fallback() {
            var ta = document.createElement('textarea');
            ta.value = text;
            ta.style.position = 'fixed'; ta.style.opacity = '0';
            document.body.appendChild(ta);
            ta.select();
            try { document.execCommand('copy'); } catch (e) { }
            document.body.removeChild(ta);
        }
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(function () { toast(okMsg); }, fallback);
        } else fallback();
    }
    function download(name, text) {
        var blob = new Blob([text], { type: 'application/json;charset=utf-8' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = name;
        document.body.appendChild(a); a.click(); document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
    }
    var toastTimer = null;
    function toast(msg) {
        var badge = document.getElementById('validateBadge');
        var old = badge.textContent, oldCls = badge.className;
        badge.textContent = msg; badge.className = 'badge ok';
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { renderJson(); }, 1600);
    }

    /* ============================================================
     * 导入
     * ========================================================== */
    var pendingCandidates = [];
    function openImport() {
        document.getElementById('importText').value = '';
        document.getElementById('importMsg').textContent = '';
        document.getElementById('importCandidates').innerHTML = '';
        pendingCandidates = [];
        document.getElementById('importModal').hidden = false;
    }

    function collectCandidates(data) {
        var out = [];
        if (Array.isArray(data)) {
            out.push({ key: '（数组，共 ' + data.length + ' 项）', name: '', arr: data });
            return out;
        }
        if (data && typeof data === 'object') {
            if (data['class']) { out.push({ key: '（单个效果）', name: data['class'], arr: [data] }); return out; }
            ['actionEntities', 'effects'].forEach(function (k) {
                if (Array.isArray(data[k])) out.push({ key: k, name: '', arr: data[k] });
            });
            if (out.length) return out;
            // 形如 { "1": { Id, Name, actionEntities: [...] }, ... }
            Object.keys(data).forEach(function (k) {
                var v = data[k];
                if (!v || typeof v !== 'object') return;
                ['actionEntities', 'effects'].forEach(function (f) {
                    if (Array.isArray(v[f])) out.push({ key: k, name: v.Name || v.name || '', arr: v[f] });
                });
            });
            if (out.length) return out;
        }
        return out;
    }

    function doImport() {
        var txt = document.getElementById('importText').value.trim();
        var msg = document.getElementById('importMsg');
        if (!txt) { msg.className = 'msg err'; msg.textContent = '请输入 JSON'; return; }
        var data;
        try { data = JSON.parse(txt); }
        catch (e) { msg.className = 'msg err'; msg.textContent = 'JSON 解析失败：' + e.message; return; }
        var cands = collectCandidates(data);
        if (!cands.length) {
            msg.className = 'msg err';
            msg.textContent = '没有找到效果数组（仅支持 actionEntities / effects；Skills.json 的 skillEffects 属于 SkillEffect 体系，本工具未覆盖）';
            return;
        }
        if (cands.length === 1) { applyImport(cands[0]); return; }
        pendingCandidates = cands;
        msg.className = 'msg ok';
        msg.textContent = '识别到 ' + cands.length + ' 个条目，请选择要导入的：';
        document.getElementById('importCandidates').innerHTML = cands.map(function (c, i) {
            return '<div class="cand" data-cand="' + i + '"><span class="k">' + esc(c.key) + '</span>' +
                '<span class="n">' + esc(c.name) + '</span><span class="hint">' + c.arr.length + ' 个效果</span></div>';
        }).join('');
    }

    function applyImport(cand) {
        State.root = cand.arr.map(function (o) { return parseNode(o, hostDef().root || 'action'); }).filter(Boolean);
        State.sel = null;
        State.collapsed = {};
        document.getElementById('importModal').hidden = true;
        renderAll();
        toast('已导入 ' + State.root.length + ' 个效果');
    }

    document.addEventListener('click', function (e) {
        var c = e.target.closest ? e.target.closest('[data-cand]') : null;
        if (c) applyImport(pendingCandidates[parseInt(c.getAttribute('data-cand'), 10)]);
    });

    /* ============================================================
     * 启动
     * ========================================================== */
    function init() {
        load();
        bind();
        updatePathHint();
        setMobileView('canvas');
        renderPalette();
        renderAll();
    }
    init();
})();
