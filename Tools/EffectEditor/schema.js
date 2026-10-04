/**
 * Sango Infinity —— 特技 / 效果编辑器 节点元数据
 * ---------------------------------------------------------------
 * 本文件描述 ActionBase / Trigger / Condition 三套体系在 JSON 中的
 * 全部可用节点（键为 C# 中 Register 的注册名，不是类名）。
 *
 * 维护约定：
 *   1. 键名必须与 C# 中 Register("xxx", ...) 的字符串完全一致；
 *   2. fields 顺序即 JSON 输出顺序（class 永远第一，slots 永远最后）；
 *   3. 新增 Action 时同步在此登记，并检查 ActionBase.Init() 的注册名；
 *   4. aliases 用于导入历史 JSON 时自动纠偏（注册名与类名不一致的情况）。
 *
 * 字段对象说明：
 *   { name, type, label, enum, def, unit, optional, required, tip }
 *   type   : int | bool | enum | enum[] | int[]
 *   unit   : percent(百分比 /100) | permille(万分比 /10000)
 *   optional: 为 true 时，若值等于默认值或空数组，则输出时省略
 *   required: 为 true 时，未填写会阻断复制并报错
 *
 * 槽位对象说明：
 *   { name, accept, kind, label, force }
 *   accept : action | trigger | condition
 *   kind   : single(输出对象) | array(输出数组)
 *   force  : 为 true 时，即使为空也强制输出（C# 未判空，缺失会空引用）
 */
(function (global) {
    'use strict';

    /* ============================================================
     * 一、枚举表
     * ========================================================== */
    var enums = {
        Operator: [
            { v: 'gte', l: '≥ 大于等于' },
            { v: 'gt', l: '> 大于' },
            { v: 'lte', l: '≤ 小于等于' },
            { v: 'lt', l: '< 小于' },
            { v: 'eq', l: '= 等于' }
        ],
        CompareSide: [
            { v: 'self', l: '主动方 self' },
            { v: 'target', l: '目标方 target' }
        ],
        CheckLand: [
            { v: 0, l: '不限（陆 + 水都检查）' },
            { v: 1, l: '仅陆地兵种' },
            { v: 2, l: '仅水上兵种' }
        ],
        AtkSide: [
            { v: 0, l: '攻击方' },
            { v: 1, l: '受击方' }
        ],
        IsNormal: [
            { v: 0, l: '都可以' },
            { v: 1, l: '一般攻击' },
            { v: 2, l: '非一般攻击（战法）' }
        ],
        IsRange: [
            { v: 0, l: '都可以' },
            { v: 1, l: '远程' },
            { v: 2, l: '近战' }
        ],
        TargetType: [
            { v: 0, l: '行动方' },
            { v: 1, l: '目标方' }
        ],
        BuildingSide: [
            { v: 0, l: '己方' },
            { v: 1, l: '敌方' },
            { v: 2, l: '所有' }
        ],
        YesNo: [
            { v: 1, l: '是' },
            { v: 0, l: '否' }
        ],
        BoolTF: [
            { v: 'true', l: 'true' },
            { v: 'false', l: 'false' }
        ],
        ModifyType: [
            { v: 0, l: '仅作用于增加量（>0）' },
            { v: 1, l: '仅作用于减少量（<0）' }
        ],
        ZocType: [
            { v: 0, l: '忽略水上 ZOC' },
            { v: 1, l: '忽略陆地 ZOC' },
            { v: 2, l: '两者都忽略' }
        ],
        GainBound: [
            { v: 0, l: '所属城市全局（所有城内建筑）' },
            { v: 1, l: '建筑周围一圈' }
        ],
        /* 兵种 kind：TroopTypes.json（同一 kind 下的特殊兵种已合并显示，值仍是 kind） */
        TroopKind: [
            { v: 1, l: '1 剑兵' }, { v: 2, l: '2 枪兵 / 青州兵 / 白毦兵 / 大戟士' }, { v: 3, l: '3 戟兵 / 陷阵营 / 虎卫军 / 藤甲兵' },
            { v: 4, l: '4 弩兵 / 无当飞军 / 锦帆军' }, { v: 5, l: '5 骑兵 / 虎豹骑 / 白马义从 / 西凉铁骑 / 象兵' }, { v: 6, l: '6 走舸 / 楼船 / 斗舰' },
            { v: 7, l: '7 运输 / 木牛' }, { v: 8, l: '8 冲车 / 木兽' }, { v: 9, l: '9 井阑 / 投石' }
        ],
        /* 兵种 kind（部队系专用，额外支持 -1 / -2 / 0 三个特殊值） */
        TroopKindSel: [
            { v: 0, l: '0 全部兵种（不限制）' }, { v: -1, l: '-1 所有陆地兵种' },
            { v: -2, l: '-2 所有水上兵种' }, { v: 1, l: '1 剑兵' },
            { v: 2, l: '2 枪兵 / 青州兵 / 白毦兵 / 大戟士' }, { v: 3, l: '3 戟兵 / 陷阵营 / 虎卫军 / 藤甲兵' },
            { v: 4, l: '4 弩兵 / 无当飞军 / 锦帆军' }, { v: 5, l: '5 骑兵 / 虎豹骑 / 白马义从 / 西凉铁骑 / 象兵' },
            { v: 6, l: '6 走舸 / 楼船 / 斗舰' }, { v: 7, l: '7 运输 / 木牛' },
            { v: 8, l: '8 冲车 / 木兽' }, { v: 9, l: '9 井阑 / 投石' }
        ],
        /* 建筑 kind：BuildingTypes.json（同一 kind 的多个等级已合并，值仍是 kind） */
        BuildingKind: [
            { v: 1, l: '1 都市' }, { v: 2, l: '2 关所' },
            { v: 3, l: '3 港' }, { v: 4, l: '4 阵 / 砦 / 城塞' },
            { v: 5, l: '5 都市(小)' }, { v: 7, l: '7 箭楼 / 连弩楼' },
            { v: 9, l: '9 土垒 / 石墙' }, { v: 11, l: '11 投石台' },
            { v: 12, l: '12 太鼓台' }, { v: 13, l: '13 军乐台' },
            { v: 14, l: '14 石兵八阵' }, { v: 15, l: '15 根据地' },
            { v: 16, l: '16 根据地' }, { v: 17, l: '17 火种' },
            { v: 18, l: '18 火焰种' }, { v: 19, l: '19 火球' },
            { v: 20, l: '20 火焰球' }, { v: 21, l: '21 火船' },
            { v: 22, l: '22 业火球' }, { v: 23, l: '23 业火种' },
            { v: 24, l: '24 落石' }, { v: 25, l: '25 堤防' },
            { v: 26, l: '26 浅滩' }, { v: 27, l: '27 山岳' },
            { v: 28, l: '28 长城' }, { v: 29, l: '29 遗迹' },
            { v: 30, l: '30 庙' }, { v: 31, l: '31 太庙' },
            { v: 32, l: '32 市场 / 市场 Lv2 / 市场 Lv3' }, { v: 33, l: '33 农场 / 农场 Lv2 / 农场 Lv3' },
            { v: 34, l: '34 兵舍 / 兵舍 Lv2 / 兵舍 Lv3' }, { v: 35, l: '35 锻冶厂 / 锻冶厂 Lv2 / 锻冶厂 Lv3' },
            { v: 36, l: '36 厩舍 / 厩舍 Lv2 / 厩舍 Lv3' }, { v: 37, l: '37 工房 / 工房 Lv2 / 工房 Lv3' },
            { v: 38, l: '38 造船厂 / 造船厂 Lv2 / 造船厂 Lv3' }, { v: 39, l: '39 造币厂' },
            { v: 40, l: '40 谷仓' }, { v: 41, l: '41 符节台' },
            { v: 42, l: '42 军事府' }, { v: 43, l: '43 人材府' },
            { v: 44, l: '44 外交府' }, { v: 45, l: '45 计略府' },
            { v: 46, l: '46 炼兵所' }, { v: 47, l: '47 大市场' },
            { v: 48, l: '48 鱼市' }, { v: 49, l: '49 黑市' },
            { v: 50, l: '50 军屯农' }
        ],
        /* 地形：TerrainTypes.json */
        TerrainType: [
            { v: 0, l: '0 无' }, { v: 1, l: '1 草地' }, { v: 2, l: '2 土' },
            { v: 3, l: '3 砂地' }, { v: 4, l: '4 湿地' }, { v: 5, l: '5 毒泉' },
            { v: 6, l: '6 森' }, { v: 7, l: '7 川' }, { v: 8, l: '8 河' },
            { v: 9, l: '9 海' }, { v: 10, l: '10 荒地' }, { v: 11, l: '11 主径' },
            { v: 12, l: '12 栈道' }, { v: 13, l: '13 渡所' }, { v: 14, l: '14 浅滩' },
            { v: 15, l: '15 岸' }, { v: 16, l: '16 崖' }, { v: 17, l: '17 都市' },
            { v: 18, l: '18 港' }, { v: 19, l: '19 关所' }, { v: 20, l: '20 小径' },
            { v: 21, l: '21 地20' }, { v: 22, l: '22 地21' }, { v: 23, l: '23 地22' },
            { v: 24, l: '24 地23' }, { v: 25, l: '25 地24' }, { v: 26, l: '26 地25' },
            { v: 27, l: '27 地26' }, { v: 28, l: '28 地27' }, { v: 29, l: '29 地28' },
            { v: 30, l: '30 地29' }, { v: 31, l: '31 地30' }
        ],
        /* 城市工作：JobTypes.json */
        JobId: [
            { v: 1, l: '1 农业' }, { v: 2, l: '2 商业' }, { v: 3, l: '3 巡查' },
            { v: 4, l: '4 训练' }, { v: 5, l: '5 搜索' }, { v: 6, l: '6 招募士兵' },
            { v: 7, l: '7 招募武将' }, { v: 8, l: '8 生产兵装' }, { v: 9, l: '9 建造' },
            { v: 10, l: '10 生产兵器' }, { v: 11, l: '11 生产船' }, { v: 12, l: '12 生产马' },
            { v: 13, l: '13 交易粮食' }, { v: 14, l: '14 派遣武将' }, { v: 15, l: '15 召唤武将' },
            { v: 16, l: '16 升级建筑' }, { v: 17, l: '17 组建部队' }, { v: 18, l: '18 组建运输部队' },
            { v: 19, l: '19 研究' }, { v: 20, l: '20 褒赏' }, { v: 21, l: '21 军事驻守' },
            { v: 22, l: '22 外交送礼' }, { v: 23, l: '23 外交同盟' }
        ],
        /* 战法：Skills.json */
        SkillId: [
            { v: 1, l: '1 近战普攻' }, { v: 2, l: '2 远程普攻' }, { v: 3, l: '3 突刺' },
            { v: 4, l: '4 螺旋突刺' }, { v: 5, l: '5 二段突刺' }, { v: 6, l: '6 熊手' },
            { v: 7, l: '7 横扫' }, { v: 8, l: '8 旋风' }, { v: 9, l: '9 火矢' },
            { v: 10, l: '10 贯箭' }, { v: 11, l: '11 乱射' }, { v: 12, l: '12 突击' },
            { v: 13, l: '13 突破' }, { v: 14, l: '14 突进' }, { v: 15, l: '15 火矢' },
            { v: 16, l: '16 破碎' }, { v: 17, l: '17 放射' }, { v: 18, l: '18 投石' },
            { v: 19, l: '19 火矢' }, { v: 20, l: '20 猛撞' }, { v: 21, l: '21 投石' },
            { v: 22, l: '22 火计' }, { v: 23, l: '23 灭火' }, { v: 24, l: '24 伪报' },
            { v: 25, l: '25 扰乱' }, { v: 26, l: '26 镇静' }, { v: 27, l: '27 伏兵' },
            { v: 28, l: '28 内讧' }, { v: 29, l: '29 落雷' }, { v: 30, l: '30 妖术' }
        ],
        /* 状态：Buffs.json */
        BuffId: [
            { v: 1, l: '1 混乱' }, { v: 2, l: '2 伪报' },
            { v: 3, l: '3 增加防御' }, { v: 4, l: '4 增加攻击' }
        ],
        /* Buffs.json 的 kind 字段 */
        BuffKind: [
            { v: 1, l: '1 异常（混乱 / 伪报 / 止步等）' },
            { v: 2, l: '2 增益（增加攻击 / 防御等）' }
        ],
        /* SkillAttackOffsetType */
        SkillAttackOffsetType: [
            { v: 0, l: '0 Customize 自定义' },
            { v: 1, l: '1 Ring 环形' },
            { v: 2, l: '2 DirectionLine 方向直线' },
            { v: 3, l: '3 SelfRing 自身环形' },
            { v: 4, l: '4 SpellNeighbors 施法相邻' },
            { v: 5, l: '5 Spiral 螺旋' },
            { v: 6, l: '6 Fan 扇形' },
            { v: 7, l: '7 Rectangle 矩形' },
            { v: 8, l: '8 Cross 十字' },
            { v: 9, l: '9 Square 方形' },
            { v: 10, l: '10 Diamond 菱形' }
        ],
        /* 部队互比属性：TroopCompareFunction */
        TroopCompareAttr: [
            { v: 'command', l: 'command 统率' }, { v: 'strength', l: 'strength 武力' },
            { v: 'intelligence', l: 'intelligence 智力' }, { v: 'politics', l: 'politics 政治' },
            { v: 'glamour', l: 'glamour 魅力' }, { v: 'age', l: 'age 年龄' },
            { v: 'level', l: 'level 等级' }, { v: 'official', l: 'official 官位' },
            { v: 'spearLv', l: 'spearLv 枪兵适性' }, { v: 'halberdLv', l: 'halberdLv 戟兵适性' },
            { v: 'crossbowLv', l: 'crossbowLv 弩兵适性' }, { v: 'rideLv', l: 'rideLv 骑兵适性' },
            { v: 'waterLv', l: 'waterLv 水军适性' }, { v: 'machineLv', l: 'machineLv 兵器适性' },
            { v: 'attack', l: 'attack 攻击力' }, { v: 'defence', l: 'defence 防御力' },
            { v: 'morale', l: 'morale 气力' }, { v: 'moveAbility', l: 'moveAbility 移动力' },
            { v: 'troops', l: 'troops 兵力' }
        ],
        /* 武将五维 */
        PersonAttr: [
            { v: 'command', l: 'command 统率' }, { v: 'strength', l: 'strength 武力' },
            { v: 'intelligence', l: 'intelligence 智力' }, { v: 'politics', l: 'politics 政治' },
            { v: 'glamour', l: 'glamour 魅力' }
        ],
        /* 城市属性 */
        CityAttr: [
            { v: 'commerce', l: 'commerce 商业' }, { v: 'agriculture', l: 'agriculture 农业' },
            { v: 'popularSupport', l: 'popularSupport 民心' }, { v: 'security', l: 'security 治安' },
            { v: 'energy', l: 'energy 气力' }, { v: 'morale', l: 'morale 士气' },
            { v: 'troops', l: 'troops 士兵数' }, { v: 'food', l: 'food 兵粮' },
            { v: 'gold', l: 'gold 资金' }, { v: 'population', l: 'population 人口' },
            { v: 'troopPopulation', l: 'troopPopulation 军役人口' }
        ],
        /* 技能目标格类型（注意：代码里 city/port/gate/building 均带取反） */
        SkillTargetCellType: [
            { v: 'troop', l: 'troop 目标是部队' },
            { v: 'buildingBase', l: 'buildingBase 目标是建筑 / 城市' },
            { v: 'cityBase', l: 'cityBase 目标是城市基类' },
            { v: 'building', l: 'building 目标不是城市基类' },
            { v: 'city', l: 'city 目标不是城市' },
            { v: 'port', l: 'port 目标不是港口' },
            { v: 'gate', l: 'gate 目标不是关所' }
        ],
        /* 势力关系（FactionCheck 仅这两个值有效） */
        FactionCheckType: [
            { v: 'ally', l: 'ally 同盟' },
            { v: 'enemy', l: 'enemy 敌方' }
        ],
        CompareResult: [
            { v: 1, l: '1 大于' },
            { v: 0, l: '0 等于' },
            { v: -1, l: '-1 小于' }
        ]
    };

    /* ============================================================
     * 二、字段模板（避免同一语义字段在多处语义漂移）
     * ========================================================== */
    function I(name, label, opt) { var f = { name: name, type: 'int', label: label, def: 0 }; for (var k in opt) f[k] = opt[k]; return f; }
    function E(name, label, enumName, def, opt) { var f = { name: name, type: 'enum', label: label, enum: enumName, def: def }; for (var k in opt) f[k] = opt[k]; return f; }
    function EA(name, label, enumName, opt) { var f = { name: name, type: 'enum[]', label: label, enum: enumName, optional: true }; for (var k in opt) f[k] = opt[k]; return f; }
    function IA(name, label, opt) { var f = { name: name, type: 'int[]', label: label, optional: true }; for (var k in opt) f[k] = opt[k]; return f; }
    function B(name, label, def, opt) { var f = { name: name, type: 'bool', label: label, def: !!def }; for (var k in opt) f[k] = opt[k]; return f; }

    /* 部队系基类 TroopActionBase：value + kinds */
    var F_TROOP_KINDS = EA('kinds', '兵种限制', 'TroopKindSel', { tip: '留空 = 不限制兵种；-1 全部陆地兵 / -2 全部水上兵 / 0 全部兵种' });
    /* 部队互击基类 TroopTroopActionBase 的过滤字段 */
    var F_CTX = [
        E('checkLand', '地形检查', 'CheckLand', 0),
        E('isDefender', '攻守方', 'AtkSide', 0, { tip: '0 攻击方 / 1 受击方（此处为 int，与 TroopChangeMorale 的 bool 版不同名同义）' }),
        E('isNormal', '攻击类型', 'IsNormal', 0),
        E('isRange', '距离类型', 'IsRange', 0)
    ];
    var S_CONDITION = { name: 'condition', accept: 'condition', kind: 'single', label: '生效条件', optional: true };

    /* 建筑系基类：BuildingActionBase 系的范围 / 阵营 */
    function F_BOUND(def) { return I('bound', '生效范围', { def: def, tip: '格数（螺旋半径）；-1 = 使用建筑自身的 effectCells' }); }
    var F_TARGET_SIDE = E('tartgetType', '目标阵营', 'BuildingSide', 0, { tip: '注意拼写为 tartgetType（源码如此）；0 己方 / 1 敌方 / 2 所有' });

    /* 建筑系（范围型）公共字段组：value + bound + tartgetType */
    function bldRange(valueField) {
        return [valueField, F_BOUND(1), F_TARGET_SIDE];
    }

    /* 势力·建筑系：ForceBuildingActionBase = value + kinds(BuildingKind) */
    function forceBld(valueField, label) {
        return [valueField, EA('kinds', '建筑类型限制', 'BuildingKind', { tip: '留空 = 不限类型' })];
    }
    /* 势力·部队系：ForceTroopActionBase = value + kinds(TroopKind) */
    function forceTroop(valueField) {
        return [valueField, EA('kinds', '兵种限制', 'TroopKind', { tip: '留空 = 全部兵种' })];
    }
    /* 部队系（TroopActionBase）：自有字段 + kinds */
    function troop(fields, kinds) {
        return fields.concat([kinds || F_TROOP_KINDS]);
    }
    /* 部队互击系（TroopTroopActionBase）：自有字段 + kinds + 过滤字段 + condition 槽 */
    function troopTroop(fields) {
        return {
            fields: fields.concat([F_TROOP_KINDS]).concat(F_CTX),
            slots: [S_CONDITION]
        };
    }

    /* ============================================================
     * 三、Action（键 = Register 注册名）
     * ========================================================== */
    var action = {};

    /* -------- 建筑 -------- */
    action['BuildingAddBuff'] = {
        label: '范围状态建筑（附加 Buff）', group: '建筑',
        desc: '建筑回合开始时，为范围内指定阵营的部队附加 / 刷新 Buff',
        fields: [
            E('value', '附加状态', 'BuffId', 1, { tip: '此处 value 语义为 BuffId' }),
            I('counter', '持续回合数', {}),
            F_BOUND(-1),
            F_TARGET_SIDE
        ],
        slots: []
    };
    action['BuildingAddTroopMorale'] = {
        label: '军乐台（范围恢复气力）', group: '建筑',
        desc: '建筑回合结束时，为范围内指定阵营部队恢复气力',
        fields: bldRange(I('value', '恢复气力值', {})),
        slots: []
    };
    action['BuildingImproveTroopAttack'] = {
        label: '太鼓台（范围内攻击提升）', group: '建筑',
        desc: '范围内己方部队攻击力提升（百分比）',
        fields: bldRange(I('value', '攻击提升', { unit: 'percent', tip: '百分比：20 = +20%' })),
        slots: []
    };
    action['BuildingImproveTroopDefence'] = {
        label: '阵 / 砦 / 城塞（范围内防御提升）', group: '建筑',
        desc: '范围内己方部队防御力提升（百分比）',
        fields: bldRange(I('value', '防御提升', { unit: 'percent', tip: '百分比：20 = +20%' })),
        slots: []
    };
    action['BuildingImproveFoodGain'] = {
        label: '谷仓（提升粮食收入）', group: '建筑',
        desc: '提升指定类型建筑的粮食收入系数',
        fields: [
            I('value', '提升系数', { unit: 'percent' }),
            EA('kinds', '影响的建筑类型', 'BuildingKind'),
            E('bound', '作用范围', 'GainBound', 0)
        ],
        slots: []
    };
    action['BuildingImproveGoldGain'] = {
        label: '造币厂（提升资金收入）', group: '建筑',
        desc: '提升指定类型建筑的资金收入系数',
        fields: [
            I('value', '提升系数', { unit: 'percent' }),
            EA('kinds', '影响的建筑类型', 'BuildingKind'),
            E('bound', '作用范围', 'GainBound', 0)
        ],
        slots: []
    };
    action['BuildingImproveFoodGainByCityTroops'] = {
        label: '军屯农（按兵力提升粮食产量）', group: '建筑',
        desc: '按所属城市士兵数量提升粮食产量，士兵越多倍率越高',
        fields: [
            I('value', '最大倍率', { unit: 'percent' }),
            I('minTroops', '士兵数下限（对应最低倍率）', {}),
            I('maxTroops', '士兵数上限（对应最大倍率）', {})
        ],
        slots: []
    };

    /* -------- 城市 -------- */
    action['CityBumperHarvest'] = {
        label: '祈愿（丰收）', group: '城市',
        desc: '春秋季初按概率给予城市丰收状态，期间粮食产量提升',
        fields: [
            I('chance', '触发概率', { unit: 'percent', tip: '百分比：0~100（GameRandom.Chance）' }),
            I('durationMonths', '持续月数', {}),
            I('foodHarvestFactor', '丰收期粮食产量', { unit: 'percent' })
        ],
        slots: []
    };
    action['CityChangeFoodCost'] = {
        label: '屯田（固定士兵粮食消耗）', group: '城市',
        desc: '把指定类型城市的士兵粮食消耗改为固定值',
        fields: [
            I('value', '粮食消耗固定值', {}),
            EA('cityKinds', '城市类型', 'BuildingKind')
        ],
        slots: []
    };
    action['CityChangeSearchingWild'] = {
        label: '眼力（探索结果固定）', group: '城市',
        desc: '把城市人才探索的结果改为固定值',
        fields: [I('value', '探索结果值', {})],
        slots: []
    };
    action['CityFoodHarvestEveryMonth'] = {
        label: '征收（每月额外粮食）', group: '城市',
        desc: '所属城市每月额外获得粮食',
        fields: [I('value', '每月粮食', { unit: 'percent', tip: '单次收入按 value / 100 / 3 计算' })],
        slots: []
    };
    action['CityGoldHarvestEveryTurn'] = {
        label: '征税（每回合额外资金）', group: '城市',
        desc: '所属城市每回合额外获得资金',
        fields: [I('value', '每回合资金', { unit: 'percent', tip: '单次收入按 value / 100 / 3 计算' })],
        slots: []
    };
    action['CityImproveFoodHarvest'] = {
        label: '米道（粮食收入提升）', group: '城市',
        desc: '城市每季粮食收入提升',
        fields: [I('value', '提升比例', { unit: 'percent' })],
        slots: []
    };
    action['CityImproveGoldHarvest'] = {
        label: '富豪（资金收入提升）', group: '城市',
        desc: '城市每月资金收入提升',
        fields: [I('value', '提升比例', { unit: 'percent' })],
        slots: []
    };
    action['CityImproveJobCounterResult'] = {
        label: '发明 / 造船（缩短工期）', group: '城市',
        desc: '指定城市工作的生产期（工期）缩短',
        fields: [
            I('value', '缩短比例', { unit: 'percent' }),
            EA('jobIds', '生效的工作', 'JobId')
        ],
        slots: []
    };
    action['CityImproveJobResult'] = {
        label: '名声 / 能吏 / 繁殖（提升产出）', group: '城市',
        desc: '指定城市工作的产出量提升',
        fields: [
            I('value', '提升比例', { unit: 'percent' }),
            EA('jobIds', '生效的工作', 'JobId')
        ],
        slots: []
    };
    action['CityImproveResearchCost'] = {
        label: '指导（研究费用降低）', group: '城市',
        desc: '城市研究技巧的费用降低',
        fields: [I('value', '降低比例', { unit: 'percent', tip: '仅作用于资金部分' })],
        slots: []
    };
    action['CityImproveSearchingWild'] = {
        label: '眼力（探索成功率提升）', group: '城市',
        desc: '提高城市人才探索成功率',
        fields: [I('value', '提升比例', { unit: 'percent' })],
        slots: []
    };
    action['CityPreventPersonLoyaltyLoss'] = {
        label: '仁政（阻止掉忠诚）', group: '城市',
        desc: '阻止本城武将换季结算时降低忠诚（无参数）',
        fields: [], slots: []
    };

    /* -------- 势力 -------- */
    action['BuildingBaseAttackBack'] = {
        label: '强化防卫（据点反击强化）', group: '势力',
        desc: '据点被攻击时反击伤害提高',
        fields: forceBld(I('value', '反击伤害提升', { unit: 'percent' })), slots: []
    };
    action['CityAddDurability'] = {
        label: '据点即时修复', group: '势力',
        desc: '生效瞬间为所有符合类型的据点补充耐久（Init 内立即执行）',
        fields: forceBld(I('value', '补充耐久', {})), slots: []
    };
    action['CityDurabilityLimit'] = {
        label: '强化城墙（耐久上限）', group: '势力',
        desc: '城池 / 关卡 / 港口耐久上限增加',
        fields: forceBld(I('value', '上限增加', {})), slots: []
    };
    action['CityFoodLimit'] = {
        label: '粮仓扩建（粮食上限）', group: '势力',
        desc: '据点粮食上限增加',
        fields: forceBld(I('value', '上限增加', {})), slots: []
    };
    action['CityGoldLimit'] = {
        label: '金库扩建（资金上限）', group: '势力',
        desc: '据点资金上限增加',
        fields: forceBld(I('value', '上限增加', {})), slots: []
    };
    action['CitySecurityChange'] = {
        label: '治安维护（治安下降减缓）', group: '势力',
        desc: '换季治安下降值按比例缩放',
        fields: forceBld(I('value', '下降值缩放', { unit: 'percent' })), slots: []
    };
    action['CityStoreLimit'] = {
        label: '仓库扩建（道具格上限）', group: '势力',
        desc: '道具仓库格数上限增加',
        fields: forceBld(I('value', '上限增加', {})), slots: []
    };
    action['CityTroopsLimit'] = {
        label: '兵营扩建（驻兵上限）', group: '势力',
        desc: '可驻扎兵力上限增加',
        fields: forceBld(I('value', '上限增加', {})), slots: []
    };
    action['ForceCityMaxMorale'] = {
        label: '熟练兵（据点部队气力上限）', group: '势力',
        desc: '势力据点部队气力上限增加',
        fields: forceBld(I('value', '上限增加', {})), slots: []
    };
    action['ForcePersonLoyaltyChange'] = {
        label: '忠诚保护（势力忠诚）', group: '势力',
        desc: '按百分比调整势力全体武将掉忠诚的概率',
        fields: [I('value', '概率缩放', { unit: 'percent' })], slots: []
    };
    action['ForceTroopMaxTroop'] = {
        label: '军制改革（兵力上限）', group: '势力',
        desc: '势力所有部队兵力上限增加',
        fields: forceTroop(I('value', '上限增加', {})), slots: []
    };
    action['TroopAddAttack'] = {
        label: '精锐兵种（攻击力 +）', group: '势力',
        desc: '指定兵种攻击力增加',
        fields: forceTroop(I('value', '攻击力增加', {})), slots: []
    };
    action['TroopAddDefence'] = {
        label: '精锐兵种（防御力 +）', group: '势力',
        desc: '指定兵种防御力增加',
        fields: forceTroop(I('value', '防御力增加', {})), slots: []
    };
    action['TroopAddDamageBuildingExtraFactor'] = {
        label: '攻城强化（对据点破坏力）', group: '势力',
        desc: '对据点 / 设施 / 建筑的战法破坏力提高',
        fields: forceTroop(I('value', '提升比例', { unit: 'percent' })), slots: []
    };
    action['TroopAddDamageTroopExtraFactor'] = {
        label: '对军强化（对敌军威力）', group: '势力',
        desc: '对敌军的战法威力提高',
        fields: forceTroop(I('value', '提升比例', { unit: 'percent' })), slots: []
    };
    action['TroopReplaceSkill'] = {
        label: '战法升级（替换战法）', group: '势力',
        desc: '把原有战法替换为新战法',
        fields: [
            E('srcSkillId', '被替换的原战法', 'SkillId', 1, { required: true }),
            E('value', '替换为的新战法', 'SkillId', 1, { required: true })
        ].concat([EA('kinds', '兵种限制', 'TroopKind')]),
        slots: []
    };
    action['TroopStealFood'] = {
        label: '袭击兵粮（夺取兵粮）', group: '势力',
        desc: '攻击时夺取敌方兵粮（value 未使用）',
        fields: [EA('kinds', '兵种限制', 'TroopKind')], slots: []
    };

    /* -------- 部队（TroopActionBase 系） -------- */
    action['TroopAddMoveAbility'] = {
        label: '强行 / 长驱（移动力提升）', group: '部队',
        desc: '指定兵种移动力提升',
        fields: troop([
            I('value', '移动力增加', {}),
            E('checkLand', '地形检查', 'CheckLand', 0)
        ]), slots: []
    };
    action['TroopChangeMorale'] = {
        label: '扫荡 / 威风 / 昂扬（气力增减）', group: '部队',
        desc: '事件发生时增减部队气力',
        fields: troop([
            I('value', '气力变化量', { tip: '正数为增加，负数为减少' }),
            E('targetType', '作用对象', 'TargetType', 0),
            B('isDefender', '以受击方判定归属', false, { tip: 'bool（本类为 bool，与 TroopTroopActionBase 的 int 版不同）；true 时用 trigger.TargetTroop 判定' })
        ]), slots: []
    };
    action['TroopComboAttack'] = {
        label: '连击（追加一次攻击）', group: '部队',
        desc: '一般攻击后额外再攻击一次',
        fields: troop([
            I('count', '连击次数上限', { required: true }),
            I('value', '保留字段（通常不填）', { optional: true })
        ]),
        slots: [
            { name: 'condition', accept: 'condition', kind: 'single', label: '触发条件', optional: true },
            { name: 'comboCondition', accept: 'condition', kind: 'single', label: '连击成立条件', optional: true }
        ]
    };
    action['TroopIgnoreFire'] = {
        label: '火神（免疫火焰）', group: '部队',
        desc: '免疫火焰伤害',
        fields: troop([E('value', '是否免疫', 'YesNo', 1)]), slots: []
    };
    action['TroopIgnoreZOC'] = {
        label: '飞将 / 遁走（无视控制区域）', group: '部队',
        desc: '忽略敌军控制区域（ZOC）',
        fields: troop([E('value', '忽略类型', 'ZocType', 2)]), slots: []
    };
    action['TroopImproveDefeatTechniquePoint'] = {
        label: '精妙（击破技巧点提升）', group: '部队',
        desc: '击破敌方部队时获得的技巧点提高',
        fields: troop([I('value', '提升比例', { unit: 'percent' })]), slots: []
    };
    action['TroopModifyFireDamage'] = {
        label: '藤甲（火攻损伤提升）', group: '部队',
        desc: '受到的火攻损伤按万分比缩放',
        fields: troop([I('value', '火焰伤害倍率', { unit: 'permille', tip: '万分比：20000 = 2 倍' })]), slots: []
    };
    action['TroopModifyMorale'] = {
        label: '诗想（气力恢复效果提升）', group: '部队',
        desc: '所有气力恢复效果按万分比缩放',
        fields: troop([
            I('value', '倍率', { unit: 'permille', tip: '万分比：20000 = 2 倍' }),
            E('modifyType', '作用方向', 'ModifyType', 0)
        ]), slots: []
    };
    action['TroopRecure'] = {
        label: '部队恢复（按比例回兵）', group: '部队',
        desc: '事件发生时按百分比恢复兵力',
        fields: troop([I('percent', '恢复比例', { unit: 'percent', required: true })]), slots: []
    };
    action['TroopSetFireDamage'] = {
        label: '火神（强化火焰伤害）', group: '部队',
        desc: '强化自身造成的火焰伤害数值',
        fields: troop([I('value', '火焰伤害倍率', { unit: 'permille' })]), slots: []
    };
    action['TroopSkillBack'] = {
        label: '反计 / 还射（战法反击）', group: '部队',
        desc: '识破后对施法部队施展同样战法反击',
        fields: troop([I('value', '保留字段（通常不填）', { optional: true })]),
        slots: [{ name: 'condition', accept: 'condition', kind: 'single', label: '触发条件', optional: true }]
    };

    /* -------- 组合（容器） -------- */
    action['TroopTriggerAction'] = {
        label: '触发器 + 效果（容器）', group: '组合',
        desc: '触发时机满足时，批量执行子效果。这是唯一可嵌套的容器型 Action',
        fields: [],
        slots: [
            { name: 'triggerList', accept: 'trigger', kind: 'array', label: '触发时机', force: true },
            { name: 'actionList', accept: 'action', kind: 'array', label: '执行效果', force: true }
        ]
    };

    /* -------- 部队互击（TroopTroopActionBase 系） -------- */
    function TT(cls, label, desc, fields) { var d = troopTroop(fields); d.label = label; d.group = '部队'; d.desc = desc; action[cls] = d; }

    TT('TroopAddBuff', '部队状态（附加 Buff）', '事件发生时为部队添加指定 Buff，持续 N 回合', [
        E('buffId', '附加状态', 'BuffId', 1, { required: true }),
        I('probability', '触发概率', { unit: 'permille', def: 10000, required: true, tip: '万分比：10000 = 100%，3000 = 30%' }),
        E('targetType', '作用对象', 'TargetType', 0),
        IA('values', '持续回合候选值', { required: true, tip: '必填（C# 未判空），例：[1, 2]' }),
        IA('weight', '候选值权重', { required: true, tip: '必填，必须与候选值数量一致，例：[70, 30]' })
    ]);
    TT('TroopAddSkill', '骑射 / 白马（追加战法）', '为兵种追加一个战法', [
        E('value', '追加的战法', 'SkillId', 1, { required: true })
    ]);
    TT('TroopAddSkillAttackRange', '火神 / 霹雳（增加命中范围）', '为战法增加命中 / 波及范围', [
        E('atkType', '范围类型', 'SkillAttackOffsetType', 1, { tip: '仅在技能未配置 atkOffsetPoint 时生效' }),
        I('addRange', '增加格数', { tip: '增量，按不同类型加到半径 / 长度 / 宽高' })
    ]);
    TT('TroopAddSkillSpellRange', '强弩 / 射程（增加施放距离）', '为战法增加施放距离', [
        I('value', '增加格数', {})
    ]);
    TT('TroopAttackBack', '还射（反击战法）', '遭受攻击时对攻击者施展指定反击战法', [
        E('value', '反击战法', 'SkillId', 1, { required: true })
    ]);
    TT('TroopChangeCaptiveFactor', '捕缚（提高捕获率）', '击破敌军时的捕获率增加', [
        I('value', '捕获率增加', {})
    ]);
    TT('TroopChangeDamage', '锻炼 / 铁壁 / 藤甲（伤害增减）', '按比例增减伤害', [
        I('value', '伤害变化', { unit: 'percent', tip: '百分比：110 = +10%，-50 = 减半' })
    ]);
    TT('TroopChangeEscapeFactor', '血路（溃灭不被俘）', '部队溃灭时同部队武将不被俘', [
        I('value', '逃跑率增加', {})
    ]);
    TT('TroopChangePersonEscapeFactor', '强运（武将不会战死 / 被俘）', '指定武将不会战死 / 被俘 / 负伤（武将来自宿主对象，非 JSON 字段）', [
        I('value', '逃跑率增加', {})
    ]);
    TT('TroopChangeSkillAttackRange', '霹雳 / 连环（设定命中范围）', '把战法命中波及范围设为指定值', [
        E('atkType', '范围类型', 'SkillAttackOffsetType', 1),
        I('atkRange', '范围格数', { tip: '覆盖值：atkOffsetPoint = { atkType, atkRange }' })
    ]);
    TT('TroopChangeSkillCost', '战法耗气增减', '为符合条件的战法增减气力消耗', [
        I('value', '气力消耗增量', { tip: '正数为增加消耗，负数为减少' })
    ]);
    TT('TroopChangeSkillSpellRange', '强弩 / 射程 / 鬼谋（设定施放距离）', '把战法施放范围设为 value 个追加格', [
        I('value', '追加格数', {})
    ]);
    TT('TroopImproveBuildPower', '筑城（建设耐久提升）', '军事设施建设耐久上升量提高', [
        I('value', '提升比例', { unit: 'percent' })
    ]);
    TT('TroopImproveSkillSuccess', '奇谋（计略成功率提升）', '对敌方部队施展计略的成功率提高', [
        I('value', '提升比例', { unit: 'percent' }),
        B('selfIsTarget', '以施法格上的部队判定', false)
    ]);
    TT('TroopResetActionOver', '二动（额外行动）', '让符合条件的兵种获得额外一次行动', [
        E('value', '额外行动次数', 'YesNo', 1)
    ]);
    TT('TroopSetDamage', '大盾 / 不屈 / 金刚（伤害固定）', '把伤害设为固定值', [
        I('value', '固定伤害值', { required: true }),
        I('belowDamage', '伤害阈值上限', { tip: '0 = 不限制；>0 时，原伤害绝对值超过该值则不生效' })
    ]);
    TT('TroopSetSkillCost', '百出（设定战法耗气）', '把部队计略气力消耗设为固定值', [
        I('value', '固定气力消耗', { required: true })
    ]);
    TT('TroopSkillCalculateAttackBack', '突袭 / 强袭 / 洞察（反击伤害调整）', '反击伤害按比例调整', [
        I('value', '伤害变化', { unit: 'percent' })
    ]);
    TT('TroopSkillCalculateCritical', '飞将 / 枪神 / 弓将（会心率）', '会心（暴击）率增加', [
        I('value', '会心率变化', { unit: 'percent', tip: '≥100 或 ≤-100 时会变为必爆 / 必不爆' }),
        B('selfIsTarget', '以施法格上的部队判定', false)
    ]);
    TT('TroopSkillCalculateSuccess', '火攻 / 言毒 / 机智（计略成功率）', '计略成功率增减', [
        I('value', '成功率变化', { unit: 'percent', tip: '≥100 或 ≤-100 时会变为必中 / 必失败' }),
        B('selfIsTarget', '以施法格上的部队判定', false)
    ]);
    TT('TroopAddFoodGain', '掠夺（击破缴获粮食）', '击破敌军时缴获粮食', [
        I('value', '缴获系数增加', {})
    ]);
    TT('TroopAddGoldGain', '掠夺（击破缴获资金）', '击破敌军时缴获资金', [
        I('value', '缴获系数增加', {})
    ]);

    /* ============================================================
     * 四、Trigger（键 = Register 注册名；Trigger 没有任何 JSON 参数）
     * ========================================================== */
    var trigger = {
        'TriggerWhenSkillHitTroop': {
            label: '技能命中部队时（伤害结算前）', group: '技能',
            desc: 'GameEvent.OnSkillDamageTroop；可覆写伤害值', fields: [], slots: []
        },
        'TriggerWhenSkillAfterHitTroop': {
            label: '技能命中部队之后', group: '技能',
            desc: 'GameEvent.OnSkillDamageTroopAfter；可覆写伤害值', fields: [], slots: []
        },
        'TriggerWhenSkillActionEnd': {
            label: '技能动作结束时', group: '技能',
            desc: 'GameEvent.OnSkillActionEnd；无伤害覆写', fields: [], slots: []
        },
        'TriggerTroopTurnStart': {
            label: '部队回合开始', group: '部队',
            desc: 'GameEvent.OnTroopTurnStart', fields: [], slots: []
        },
        'TriggerTroopOnMoralChange': {
            label: '部队士气变化时', group: '部队',
            desc: 'GameEvent.OnTroopChangeMorale；可覆写士气变化量', fields: [], slots: []
        },
        'TriggerTroopDestroyTroop': {
            label: '部队被击毁时', group: '部队',
            desc: 'GameEvent.OnTroopDestroyed；仅当击毁者为技能实例时响应',
            fields: [], slots: []
        }
    };

    /* ============================================================
     * 五、Condition（键 = Register 注册名）
     * ========================================================== */
    var condition = {};

    /* 逻辑容器 */
    condition['andList'] = {
        label: '全部满足（列表）', group: '逻辑',
        desc: '所有子条件都满足才成立；空列表视为成立',
        fields: [],
        slots: [{ name: 'list', accept: 'condition', kind: 'array', label: '条件列表', force: true, tip: '必须输出 list（哪怕为空数组），C# 未判空' }]
    };
    condition['orList'] = {
        label: '任一满足（列表）', group: '逻辑',
        desc: '任一子条件满足即成立；空列表视为不成立',
        fields: [],
        slots: [{ name: 'list', accept: 'condition', kind: 'array', label: '条件列表', force: true, tip: '必须输出 list（哪怕为空数组），C# 未判空' }]
    };
    condition['and'] = {
        label: '并且（左右两项）', group: '逻辑',
        desc: '左右两个条件都满足才成立（固定 L / R 两个字段，不是数组）',
        fields: [],
        slots: [
            { name: 'L', accept: 'condition', kind: 'single', label: '左条件' },
            { name: 'R', accept: 'condition', kind: 'single', label: '右条件' }
        ]
    };
    condition['or'] = {
        label: '或者（左右两项）', group: '逻辑',
        desc: '左右两个条件任一满足即成立（固定 L / R 两个字段，不是数组）',
        fields: [],
        slots: [
            { name: 'L', accept: 'condition', kind: 'single', label: '左条件' },
            { name: 'R', accept: 'condition', kind: 'single', label: '右条件' }
        ]
    };

    /* 通用 */
    condition['ProbabilityCheck'] = {
        label: '概率判定', group: '通用',
        desc: '按万分比随机判定，不依赖任何上下文对象',
        fields: [I('probability', '概率', { unit: 'permille', required: true, tip: '万分比：3000 = 30%，10000 = 必中' })],
        slots: []
    };
    condition['DistanceCheck'] = {
        label: '距离检查', group: '通用',
        desc: '行动格与目标格之间的距离检查（固定用 Cell.Distance）',
        fields: [
            I('distance', '距离阈值', { required: true }),
            E('operator', '比较符', 'Operator', 'eq', { tip: '默认为 eq（本类默认值与其它类不同）' })
        ],
        slots: []
    };

    /* 部队 */
    condition['TroopAttributeCompare'] = {
        label: '部队属性互比（攻方 vs 受方）', group: '部队',
        desc: '两支部队的同一属性互相比较；受方不存在时视为成立',
        fields: [
            E('attType', '比较属性', 'TroopCompareAttr', 'command', { tip: 'rideLv 在源码中的注册键带尾随空格，若失效请反馈' }),
            E('result', '期望结果', 'CompareResult', 0)
        ],
        slots: []
    };
    condition['TroopMoraleCheck'] = {
        label: '部队士气检查', group: '部队',
        desc: '比较指定部队的 morale',
        fields: [
            E('compareTarget', '检查对象', 'CompareSide', 'self'),
            I('value', '士气阈值', { required: true }),
            E('operator', '比较符', 'Operator', 'gte')
        ],
        slots: []
    };
    condition['TroopStatusCheck'] = {
        label: '部队状态检查（Buff 类型）', group: '部队',
        desc: '判断部队是否拥有某类状态（Buff kind）',
        fields: [
            E('statusType', '状态类型', 'BuffKind', 1, { required: true }),
            E('compareTarget', '检查对象', 'CompareSide', 'self'),
            B('hasStatus', '期望拥有该状态', true)
        ],
        slots: []
    };
    condition['TroopAroundTeammateCount'] = {
        label: '周围同势力友军数量', group: '部队',
        desc: '统计检查对象周围的同势力友军数量（同势力基准恒为行动方）',
        fields: [
            I('count', '数量阈值', { required: true }),
            E('compareTarget', '检查对象', 'CompareSide', 'self'),
            E('operator', '比较符', 'Operator', 'gte')
        ],
        slots: []
    };

    /* 战法 */
    condition['SkillIsCritical'] = {
        label: '是否会心一击', group: '战法',
        desc: '判断本次是否触发会心（tempCriticalFactor > 100）',
        fields: [E('result', '期望结果', 'YesNo', 1)], slots: []
    };
    condition['SkillIsNormalSkill'] = {
        label: '是否一般攻击', group: '战法',
        desc: '判断本次技能是否为一般攻击',
        fields: [E('result', '期望结果', 'YesNo', 1)], slots: []
    };
    condition['SkillIsStrategySkill'] = {
        label: '是否计略', group: '战法',
        desc: '判断本次技能是否为计略',
        fields: [E('result', '期望结果', 'YesNo', 1)], slots: []
    };
    condition['SkillIdInList'] = {
        label: '战法 ID 在列表中', group: '战法',
        desc: '判断本次战法 ID 是否在指定列表内',
        fields: [
            IA('ids', '战法 ID 列表', { required: true, force: true, tip: '必须输出 ids（哪怕为空数组），C# 未判空' }),
            E('result', '期望结果', 'YesNo', 1)
        ],
        slots: []
    };
    condition['SkillTargetCellCheck'] = {
        label: '目标格类型检查', group: '战法',
        desc: '检查目标格上的对象类型（注意 building / city / port / gate 在源码中带取反）',
        fields: [
            E('targetType', '目标类型', 'SkillTargetCellType', 'troop'),
            E('result', '期望结果', 'YesNo', 1)
        ],
        slots: []
    };

    /* 武将 */
    condition['PersonAttributeCompare'] = {
        label: '武将属性检查', group: '武将',
        desc: '比较武将五维（注册名 PersonAttributeCompare，类名 PersonAttributeCheck）',
        aliases: ['PersonAttributeCheck'],
        fields: [
            E('attributeType', '属性', 'PersonAttr', 'command'),
            E('compareTarget', '检查对象', 'CompareSide', 'self'),
            I('value', '阈值', { required: true }),
            E('operator', '比较符', 'Operator', 'gte')
        ],
        slots: []
    };
    condition['PersonLevelCheck'] = {
        label: '武将等级检查', group: '武将',
        desc: '比较武将等级（Person.Level.Id）',
        fields: [
            E('checkTarget', '检查对象', 'CompareSide', 'self'),
            I('level', '等级阈值', { required: true }),
            E('operator', '比较符', 'Operator', 'gte')
        ],
        slots: []
    };
    condition['PersonLoyaltyCheck'] = {
        label: '武将忠诚检查', group: '武将',
        desc: '比较武将忠诚',
        fields: [
            E('checkTarget', '检查对象', 'CompareSide', 'self'),
            I('value', '忠诚阈值', { required: true }),
            E('operator', '比较符', 'Operator', 'gte')
        ],
        slots: []
    };

    /* 地形 / 天气 / 势力 / 城市 */
    condition['TerrainCheck'] = {
        label: '地形检查', group: '地图',
        desc: '比较目标格的地形类型 ID',
        fields: [
            E('terrainType', '地形', 'TerrainType', 1, { required: true }),
            E('checkTarget', '检查对象', 'CompareSide', 'self')
        ],
        slots: []
    };
    condition['WeatherCheck'] = {
        label: '天气检查', group: '地图',
        desc: '比较目标格天气（weatherType 目前为 int，功能待实现）',
        fields: [
            I('weatherType', '天气类型 ID', { required: true }),
            E('checkTarget', '检查对象', 'CompareSide', 'self')
        ],
        slots: []
    };
    condition['FactionCheck'] = {
        label: '势力关系检查', group: '势力',
        desc: '检查行动势力与目标势力的关系（仅 ally / enemy 有效，其它值恒为 false）',
        fields: [E('checkType', '关系', 'FactionCheckType', 'enemy', { required: true })],
        slots: []
    };
    condition['CityAttributeCheck'] = {
        label: '城市属性检查', group: '城市',
        desc: '比较城市属性值',
        fields: [
            E('attributeType', '属性', 'CityAttr', 'commerce'),
            E('checkTarget', '检查对象', 'CompareSide', 'self'),
            I('value', '阈值', { required: true }),
            E('operator', '比较符', 'Operator', 'gte')
        ],
        slots: []
    };

    /* ============================================================
     * 六、宿主（决定根容器允许的根节点类型与提示文案）
     * ========================================================== */
    var hosts = [
        { id: 'feature', label: '特技 Features', file: 'Features.json', path: 'Features.<Id>.actionEntities', root: 'action' },
        { id: 'technique', label: '技法 Techniques', file: 'Techniques.json', path: 'Techniques.<Id>.effects', root: 'action' },
        { id: 'building', label: '建筑 BuildingTypes', file: 'BuildingTypes.json', path: 'BuildingTypes.<Id>.actionEntities', root: 'action' },
        { id: 'raw', label: '通用效果数组', file: '', path: 'actionEntities', root: 'action' }
    ];

    global.SCHEMA = {
        version: 1,
        updated: '2026-09-15',
        enums: enums,
        kinds: { action: action, trigger: trigger, condition: condition },
        hosts: hosts,
        /* 已知不可用的类名（未注册或已注释），导入时会给出明确提示 */
        blacklist: {
            'TroopAttributeCheck': '未注册（Condition.Init 中无此项）',
            'TerrainHeightCheck': '未注册，且与 TerrainCheck 实现完全相同',
            'SkillIsSuccess': '未注册，且与 SkillIsCritical 实现完全相同',
            'ResourceCheck': '整文件已注释',
            'TimeCheck': '整文件已注释',
            'BuildingImproveTroopFoodCost': '未注册',
            'TroopSetSkillSpellRange': '未注册',
            'TriggerList': '未注册且未被引用'
        }
    };
})(window);
