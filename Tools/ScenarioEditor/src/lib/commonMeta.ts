/**
 * 公共数据表（Build/Content/Data/Common）的元数据
 *
 * 这里集中三件事：
 * 1. 每个文件的展示信息（中文名、分组、说明、新增条目模板）；
 * 2. 字段名 -> 中文标签的字典（未命中时回退为按键名拆词显示原始英文键）；
 * 3. 字段的引用关系（用于把数字 ID 显示成名称，并提供下拉选择）。
 *
 * 注意：本文件对 27 个 json 是「可选增强」——即使某个文件/字段没有登记，
 * 通用编辑器依然能正常浏览与编辑，只是标签为原始键名、引用不做名称转换。
 */
import type { CollectionKey } from './types'

/* ------------------------------------------------------------------ */
/* 引用关系                                                            */
/* ------------------------------------------------------------------ */

/** 静态候选表（无对应 json，直接写在代码里的枚举） */
export interface StaticEntry {
  id: number
  name: string
}

/** 引用目标：剧本内的集合 / 公共数据表选项表 / 代码内静态枚举 */
export type RefSpec = (
  | { source: 'scenario'; collection: CollectionKey }
  | { source: 'options'; key: string }
  | { source: 'static'; entries: StaticEntry[] }
) & {
  /**
   * 数组是「按被引用表逐项索引」的，而不是「元素本身是引用」。
   *
   * 典型例子：TroopType.moveCost 的第 i 项对应 TerrainType 的 Id=i。
   * 此时显示的是「下标对应的名称 = 值」，逐个元素套名称会全部错位。
   */
  indexed?: boolean
}

/** 快捷构造函数：剧本集合引用 */
const scn = (collection: CollectionKey): RefSpec => ({ source: 'scenario', collection })
/** 快捷构造函数：公共数据表引用 */
const opt = (key: string): RefSpec => ({ source: 'options', key })
/** 快捷构造函数：静态枚举 */
const st = (entries: StaticEntry[]): RefSpec => ({ source: 'static', entries })
/** 快捷构造函数：按被引用表逐项索引的数组 */
const idx = (spec: RefSpec): RefSpec => ({ ...spec, indexed: true })

/**
 * 五维属性索引。
 *
 * 对应 C# Person.GetAttribute(attrType)：0 统率 / 1 武力 / 2 智力 / 3 政治 / 4 魅力。
 * Techniques.needAttr、BuildingTypes.effectAttrType、TroopTypes.influenceAbility 用这套下标。
 */
export const ATTRIBUTE_ENTRIES: StaticEntry[] = [
  { id: 0, name: '统率' },
  { id: 1, name: '武力' },
  { id: 2, name: '智力' },
  { id: 3, name: '政治' },
  { id: 4, name: '魅力' },
]

/* ------------------------------------------------------------------ */
/* 文件元数据                                                          */
/* ------------------------------------------------------------------ */

/** 文件元数据 */
export interface CommonFileMeta {
  /** 文件名 */
  name: string
  /** 中文标题 */
  label: string
  /** 分组（用于左侧列表归类） */
  group: string
  /** 一句话说明 */
  desc?: string
  /** 新增记录时的模板；缺省时使用 {} 并允许自由加键 */
  template?: Record<string, unknown>
  /** 记录的主键字段名，默认 Id */
  idField?: string
  /** 记录的名称字段名，默认 Name */
  nameField?: string
  /** 编辑注意事项 */
  notes?: string[]
  /**
   * 是否从「公共数据」文件列表中隐藏。
   *
   * 用于有独立维护入口、或体量过大拖慢清单的文件。隐藏后依然可以按名字直接
   * 调用接口读写，只是不在列表里出现。服务端 server/lib/commonFiles.js 的
   * HIDDEN_FILES 必须与此保持一致。
   */
  hidden?: boolean
}

/** 分组顺序 */
export const COMMON_GROUPS = ['武将与人材', '兵种与战斗', '都市与生产', '科技与能力', 'AI 与行为'] as const

/** 全部文件元数据 */
export const COMMON_FILE_META: CommonFileMeta[] = [
  // ---------------- 武将与人材 ----------------
  {
    // 内置武将库：有独立维护入口（武将库网站 + 「从武将库导入 / 回填」），
    // 且文件 1.2MB / 800+ 条，放在公共数据列表里既重复又拖慢清单解析，故隐藏。
    name: 'PersonLibrary.json',
    label: '内置武将库',
    group: '武将与人材',
    desc: '游戏内置的全量武将数据，是新剧本武将的默认来源',
    idField: 'Id',
    nameField: 'Name',
    hidden: true,
    notes: [
      '此文件条目较多（800+），表格使用虚拟滚动',
      '剧本里的武将与这里的武将通过 Id 对应，修改会影响所有依赖该武将的剧本',
    ],
  },
  {
    name: 'PersonLevels.json',
    label: '武将等级',
    group: '武将与人材',
    desc: '等级 -> 带兵数与所需经验',
    idField: 'Id',
  },
  { name: 'Officials.json', label: '官职', group: '武将与人材', desc: '官职 -> 带兵上限、俸禄与功绩要求' },
  { name: 'Titles.json', label: '爵位', group: '武将与人材', desc: '爵位 -> 带兵上限与等级' },
  {
    name: 'Personalities.json',
    label: '性格',
    group: '武将与人材',
    desc: '性格对各系统的加减成（含注释，保存会保留注释）',
  },
  { name: 'Argumentations.json', label: '义理', group: '武将与人材', desc: '义理 -> 舌战与会话相关修正' },
  {
    name: 'duelPersonBehaviour.json',
    label: '单挑行为',
    group: '武将与人材',
    desc: '武将 / 特技的单挑行为与倍率配置',
    idField: 'personId',
    nameField: 'name',
    template: { personId: 0, name: '' },
    notes: ['数组条目按声明顺序生效，专属条目优先于特技条目'],
  },
  {
    name: 'debatePersonBehaviour.json',
    label: '舌战行为',
    group: '武将与人材',
    desc: '武将 / 特技的舌战行为与倍率配置',
    idField: 'personId',
    nameField: 'name',
    template: { personId: 0, name: '' },
  },

  // ---------------- 兵种与战斗 ----------------
  {
    name: 'TroopTypes.json',
    label: '兵种',
    group: '兵种与战斗',
    desc: '兵种攻防、移动力、地形消耗与可选特技',
    notes: ['moveCost 长度需与 TerrainTypes 数量一致（每格的地形消耗）'],
  },
  { name: 'Skills.json', label: '特技', group: '兵种与战斗', desc: '战法/计略的伤害、消耗与释放范围' },
  { name: 'TroopAnimations.json', label: '部队动画', group: '兵种与战斗', desc: '部队模型动画的贴图与播放参数' },
  { name: 'Buffs.json', label: '状态效果', group: '兵种与战斗', desc: 'Buff 的种类、时限与效果列表' },
  { name: 'TerrainTypes.json', label: '地形', group: '兵种与战斗', desc: '地形产出、可燃性与移动消耗基准' },

  // ---------------- 都市与生产 ----------------
  {
    name: 'BuildingTypes.json',
    label: '建筑与设施',
    group: '都市与生产',
    desc: '都市 / 关所 / 港口的属性，以及内政设施的产出',
    notes: ['needTech 指向已研究的技巧 Id'],
  },
  { name: 'CityLevelTypes.json', label: '都市规模', group: '都市与生产', desc: '规模 -> 各类上限与槽位加成' },
  { name: 'Provinces.json', label: '州', group: '都市与生产', desc: '州 -> 所属地方（Region）与相邻州' },
  { name: 'Regions.json', label: '地方', group: '都市与生产', desc: '地方划分（中原、河北、西凉等）' },
  { name: 'ItemTypes.json', label: '物品', group: '都市与生产', desc: '道具的种类、价格与效果' },
  { name: 'JobTypes.json', label: '内政指令', group: '都市与生产', desc: '内政工作的消耗、行动力与收益' },

  // ---------------- 科技与能力 ----------------
  { name: 'Features.json', label: '特技（特性）', group: '科技与能力', desc: '武将特技的定义与触发效果' },
  { name: 'Techniques.json', label: '技巧', group: '科技与能力', desc: '技巧树：前置、消耗与效果' },
  { name: 'AttributeChangeTypes.json', label: '能力成长类型', group: '科技与能力', desc: '能力成长曲线类型' },
  { name: 'AbilityLevelTypes.json', label: '适性等级', group: '科技与能力', desc: '兵种适性等级 -> 所需经验' },
  { name: 'Flags.json', label: '势力旗帜', group: '科技与能力', desc: '旗帜配色' },

  // ---------------- AI 与行为 ----------------
  {
    name: 'AIConfig.json',
    label: 'AI 参数配置',
    group: 'AI 与行为',
    desc: '城池命令优先级、评分权重、态势判定等全局 AI 参数',
    notes: ['该文件带大量中文注释，保存时会保留注释与原有排版', '支持部分覆盖：只写想改的字段即可'],
  },
  {
    name: 'ai.json',
    label: 'AI 官职表',
    group: 'AI 与行为',
    desc: '按官职给出的带兵上限与能力加成（Officials 与 ai 两张表）',
    notes: ['本文件含 Officials 与 ai 两个分区，请在顶部切换'],
  },
  {
    name: 'RecommendedTroopTeams.json',
    label: '推荐出征队伍',
    group: 'AI 与行为',
    desc: '推荐组队模板（固定武将 / 固定特技 / 条件补位）',
    idField: 'name',
    nameField: 'name',
    template: { memberPersonIds: [], featureIds: [], fill: { command: 0, strength: 0, intelligence: 0, featureIds: [] } },
    notes: ['当前文件中示例全部被注释掉，teams 为空数组；新增条目会按模板生成'],
  },
]

/** 文件名 -> 元数据 */
export const COMMON_META_BY_NAME: Record<string, CommonFileMeta> = Object.fromEntries(
  COMMON_FILE_META.map((m) => [m.name, m])
)

/**
 * 已从「公共数据」列表中隐藏的文件名。
 *
 * 服务端 server/lib/commonFiles.js 的 HIDDEN_FILES 与这里的 hidden 标记保持一致；
 * 前端再做一层过滤，避免服务端未重启时旧清单把已隐藏的文件带出来。
 */
export const COMMON_HIDDEN_NAMES: ReadonlySet<string> = new Set(
  COMMON_FILE_META.filter((m) => m.hidden).map((m) => m.name)
)

/** 已隐藏文件的中文名，用于在界面上说明「为什么少了一个文件」 */
export const COMMON_HIDDEN_LABELS: string[] = COMMON_FILE_META.filter((m) => m.hidden).map(
  (m) => `${m.label}（${m.name}）`
)

/* ------------------------------------------------------------------ */
/* 字段标签字典                                                        */
/* ------------------------------------------------------------------ */

/** 全局字段标签（跨文件共用，优先命中） */
export const COMMON_FIELD_LABELS: Record<string, string> = {
  // 通用
  Id: 'ID',
  Name: '名称',
  name: '名称',
  desc: '说明',
  description: '描述',
  effect_desc: '效果说明',
  limitDesc: '限制说明',
  kind: '类别',
  subKind: '子类别',
  majorType: '主类别',
  limitType: '限制类型',
  level: '等级',
  exp: '所需经验',
  only: '唯一',
  enabled: '启用',
  limit: '上限',
  cost: '费用',
  path: '路径',
  icon: '图标',
  model: '模型',
  asset: '资源',
  color: '颜色',
  store: '可存放',
  storeKind: '存放类别',
  nextId: '升级为',
  p1: '预留参数 1',
  offset: '偏移',
  radius: '范围',

  // 归属与引用
  BelongForce: '所属势力',
  BelongCorps: '所属军团',
  BelongCity: '所属城市',
  FeatureList: '特技列表',
  LikePersonList: '喜欢武将',
  HatePersonList: '厌恶武将',
  SpouseList: '配偶',
  Father: '父亲',
  Mother: '母亲',
  Brother: '兄弟',
  Official: '官职',
  Level: '等级',
  Region: '所属地方',
  neighbors: '相邻州',
  personId: '武将 ID',
  featureId: '特技 ID',
  aiTable: 'AI 表',
  validItemId: '限制物品',
  validTechId: '限制技巧',
  needItem: '需要物品',
  needTech: '需要技巧',
  needAttr: '需要能力类型',
  linkBuildingKind: '关联建筑类别',
  createBuildingKind: '可建建筑类别',
  jobId: '关联内政指令',
  shadowLogForceId: '日志追踪势力',
  shadowLogCorpsId: '日志追踪军团',

  // 武将字段
  familyName: '姓',
  giveName: '名',
  nickName: '字',
  sex: '性别',
  appearance: '登场年',
  yearBorn: '出生年',
  yearDead: '死亡年',
  compatibility: '相性',
  state: '身分',
  loyalty: '忠诚',
  command: '统率',
  strength: '武力',
  intelligence: '智力',
  politics: '政治',
  glamour: '魅力',
  argumentation: '义理',
  personality: '性格',
  spearLv: '枪兵适性',
  halberdLv: '戟兵适性',
  crossbowLv: '弩兵适性',
  rideLv: '骑兵适性',
  waterLv: '水军适性',
  machineLv: '兵器适性',
  headIconID: '头像 ID',
  imageID: '立绘 ID',
  image: '立绘文件',
  image_old: '旧立绘文件',

  // 都市 / 建筑
  atk: '攻击力',
  atkBack: '反击力',
  atkRange: '攻击范围',
  damageBounds: '伤害上下限',
  durabilityLimit: '耐久上限',
  goldGain: '金收益',
  foodGain: '粮收益',
  techGain: '技巧点收益',
  costGold: '消耗资金',
  costTechPoint: '消耗技巧点',
  buffRange: '加成范围',
  workerLimit: '工作人数上限',
  buildNumLimit: '建造数量上限',
  modelBroken: '损坏模型',
  modelCreate: '建造中模型',
  product: '产物',
  productCost: '产物消耗',
  productItems: '产物物品',
  canBuild: '可建造',
  canFire: '可点火',
  effectAttrType: '效果能力类型',
  actionEntities: '效果实体',
  buffEffects: '效果列表',
  agricultureLimitAdd: '农业上限加成',
  commerceLimitAdd: '商业上限加成',
  durabilityLimitAdd: '耐久上限加成',
  foodLimitAdd: '兵粮上限加成',
  goldLimitAdd: '资金上限加成',
  insideSlotAdd: '城内槽位加成',
  outsideSlotAdd: '城外槽位加成',
  villageSlotAdd: '村庄槽位加成',
  storeLimitAdd: '库存上限加成',
  troopsLimitAdd: '兵力上限加成',
  baseGainFoodAdd: '基础粮收入加成',
  baseGainGoldAdd: '基础金收入加成',

  // 地形
  foodDeposit: '兵粮储量',
  goldDeposit: '资金储量',
  fertility: '肥沃度区间',
  prosperity: '繁荣度区间',
  foodRate: '兵粮系数',
  foodRegainDays: '恢复天数',
  fireDamageRate: '火伤害系数',
  fireRate: '可燃率',
  viewThrough: '可穿透视野',
  isWater: '水域',
  moveable: '可通行',
  canAmbush: '可伏兵',
  baseCost: '基础移动消耗',

  // 兵种
  skills: '可用特技',
  def: '防御力',
  durabilityDmg: '耐久伤害',
  move: '移动力',
  defType: '防御类型',
  atkType: '攻击类型',
  influenceAbility: '影响能力',
  foodCostFactor: '兵粮消耗系数',
  isRange: '远程',
  isFight: '可交战',
  isSingle: '单体',
  isLand: '陆上',
  ambushCriticalAdd: '伏兵会心加成',
  moveCost: '地形移动消耗',
  aniIds: '动画 ID',
  moveSound: '移动音效',
  costItems: '消耗物品',
  matchFeatures: '匹配特技',

  // 特技 / 战法
  atkDurability: '耐久伤害',
  atkOffsetPoint: '攻击偏移点',
  blockFactor: '格挡系数',
  canDamageTroop: '可伤部队',
  canDamageMachine: '可伤兵器',
  canDamageBoat: '可伤舰船',
  canDamageBuilding: '可伤建筑',
  canDamageTeam: '可伤小队',
  canSpellToCell: '可对格子释放',
  onlySpellToTeam: '仅对小队释放',
  canTriggerDuel: '可触发单挑',
  costEnergy: '消耗气力',
  successRate: '成功率',
  visualType: '演出类型',
  successMethod: '成功判定方法',
  criticalMethod: '会心判定方法',
  rangeFilterMethod: '范围筛选方法',
  spellRanges: '释放范围',
  spellConditionMethod: '释放条件方法',
  skillEffects: '效果列表',
  offsetAction: '位移动作',
  personSound: '武将音效',
  actionSound: '动作音效',
  needAblilityLevel: '需要适性等级',

  // 技巧 / 成长
  col: '列',
  row: '行',
  tabColor: '页签颜色',
  techPointCost: '消耗技巧点',
  counter: '研究耗时',
  effects: '效果列表',
  goldCost: '消耗资金',

  // 内政指令
  costAP: '消耗行动力',
  meritGain: '功绩收益',
  tpGain: '技巧点收益',
  troopsLimit: '带兵上限',
  meritNeeds: '所需功绩',
  commandNeed: '所需统率',
  strengthNeed: '所需武力',
  intelligenceNeed: '所需智力',
  politicsNeed: '所需政治',
  glamourNeed: '所需魅力',
  commandAdd: '统率加成',
  strengthAdd: '武力加成',
  intelligenceAdd: '智力加成',
  politicsAdd: '政治加成',
  glamourAdd: '魅力加成',
  levelNeed: '所需等级',
  ai: 'AI 官职表',
  Officials: '官职表',

  // 单挑 / 舌战
  duelStrengthAdd: '单挑武力加成',
  duelStrengthAge: '单挑武力年龄系数',
  duelStrengthVirtual: '单挑虚拟武力',
  criticalChanceAdd: '会心几率加成',
  criticalChanceVirtual: '会心几率虚拟值',
  criticalAge: '会心年龄阈值',
  firstChanceAdd: '先手几率加成',
  opponentFirstChanceAdd: '对手先手几率加成',
  ftkChanceAdd: '一击必杀几率加成',
  ftkChanceVirtual: '一击必杀虚拟值',
  ftkAge: '一击必杀年龄阈值',
  ftkImmune: '免疫一击必杀',
  alwaysBlockWith: '总是格挡方式',
  attackDamageMulNum: '攻击伤害倍率（分子）',
  attackDamageMulDen: '攻击伤害倍率（分母）',
  specialDamageMul: '特殊伤害倍率',
  specialHitMul: '特殊命中倍率',
  attackMulNum: '攻击倍率（分子）',
  attackMulDen: '攻击倍率（分母）',
  hpDamageMulNum: '体力伤害倍率（分子）',
  hpDamageMulDen: '体力伤害倍率（分母）',
  allRhetoric: '掌握全部话术',
  rhetoricAdd: '话术追加',
  rhetoricOverride: '话术覆盖',
  criticalForce: '会心强制结果',
  criticalPushOnBias: '优势时推送会心',

  // 性格
  falseReportSuccessAdd: '伪报成功率加成',
  disturbSuccessAdd: '搅乱成功率加成',
  calmdownSuccessAdd: '镇静成功率加成',
  ambushSuccessAdd: '伏兵成功率加成',
  sorcerySuccessAdd: '妖术成功率加成',
  infightingSuccessAdd: '内讧成功率加成',
  falseReportCriticalAdd: '伪报会心加成',
  disturbCriticalAdd: '搅乱会心加成',
  calmdownCriticalAdd: '镇静会心加成',
  sorceryCriticalAdd: '妖术会心加成',
  infightingCriticalAdd: '内讧会心加成',
  warTendencyAdd: '战争倾向',
  defenseTendencyAdd: '防守倾向',
  diplomacyTendencyAdd: '外交倾向',
  economicTendencyAdd: '经济倾向',
  technologyTendencyAdd: '技术倾向',
  recruitCaptiveTendencyAdd: '招募俘虏倾向',
  releaseCaptiveTendencyAdd: '释放俘虏倾向',
  ransomCaptiveTendencyAdd: '索赎俘虏倾向',
  captiveDealAdd: '俘虏交涉加成',
  negotiationAdd: '交涉加成',
  troopAttackScale: '部队攻击系数',
  troopCounterScale: '部队反击系数',
  troopRetreatAdd: '部队撤退倾向',
  troopAggression: '部队侵略性',
  troopBestNAdd: '部队最优目标数',
  troopSkillScale: '部队战法系数',
  troopRoleOverrideAdd: '部队角色覆盖',
  duelAcceptAdd: '接受单挑倾向',
  duelRetreatAdd: '单挑撤退倾向',
  domesticFarmingScale: '农业系数',
  domesticDevelopScale: '开发系数',
  domesticSecurityScale: '治安系数',
  domesticTrainScale: '训练系数',
  domesticSearchScale: '搜索系数',
  domesticRecruitTroopScale: '征兵系数',
  domesticRecruitPersonScale: '人才招募系数',
  domesticCreateItemScale: '制作物品系数',
  domesticCreateMachineScale: '制造兵器系数',
  domesticCreateBoatScale: '建造舰船系数',
  domesticBuildScale: '建造系数',
  domesticTradeScale: '交易系数',
  domesticRepairScale: '修复系数',
  growthScale: '成长系数',
  learnSpeedScale: '学习速度系数',
  loyaltyDriftAdd: '忠诚漂移',
  loyaltyKeepAdd: '忠诚保持',
  giftEffectAdd: '赠礼效果',
  revoltRiskAdd: '叛乱风险',
  infightingCriticalAdd2: '内讧会心加成',
  loyaltyAdd: '忠诚加成',

  // AI 参数：基础权重
  baseAIAttack: '基础权重 · 攻击',
  baseAIReinforce: '基础权重 · 增援',
  baseAITradeFood: '基础权重 · 交易兵粮',
  baseAIIntrior: '基础权重 · 内政',
  baseAISecurity: '基础权重 · 治安',
  baseAITrainTroop: '基础权重 · 训练',
  baseAIRewardPerson: '基础权重 · 赏赐',
  baseAIRecruitTroop: '基础权重 · 征兵',
  baseAICreateItems: '基础权重 · 制作物品',
  baseAICreateMachine: '基础权重 · 制造兵器',
  baseAICreateBoat: '基础权重 · 建造舰船',
  baseAISearching: '基础权重 · 搜索',
  baseAIRecruitPerson: '基础权重 · 招募人才',
  baseAITransfrom: '基础权重 · 输送',
  baseAITransfromToBelongCity: '基础权重 · 输送回本城',
  baseAIResearch: '基础权重 · 研究技巧',
  baseAIMakeSupplyTroop: '基础权重 · 组建运输队',

  // AI 参数：态势
  useTierStrategy: '启用态势策略',
  useTierRetreat: '启用态势撤退',
  tierScanRange: '态势扫描范围',
  tierDecisivePercent: '态势阈值 · 决胜',
  tierAdvantagedPercent: '态势阈值 · 优势',
  tierEvenPercent: '态势阈值 · 均势',
  tierDisadvantagedPercent: '态势阈值 · 劣势',
  tierCritical: '态势修正 · 危急',
  tierDisadvantaged: '态势修正 · 劣势',
  tierEven: '态势修正 · 均势',
  tierAdvantaged: '态势修正 · 优势',
  tierDecisive: '态势修正 · 决胜',
  cityOrder: '城池命令优先级',
  deployment: '部署开关',

  // AI 参数：填充与阀值
  fillCritical: '填充阈值 · 危急',
  fillLow: '填充阈值 · 偏低',
  fillHigh: '填充阈值 · 偏高',
  fillFull: '填充阈值 · 充足',
  troopLowFill: '兵力填充 · 偏低',
  troopMidFill: '兵力填充 · 中等',
  troopHighFill: '兵力填充 · 偏高',
  troopExpectFill: '兵力期望填充',
  foodExpectFill: '兵粮期望填充',
  goldExpectFill: '资金期望填充',
  recruitFillTarget: '征兵填充目标',
  logisticsFoodFillTarget: '后勤兵粮填充目标',
  logisticsSecurityTarget: '后勤治安目标',
  moraleFillTarget: '士气填充目标',
  moraleLow: '士气偏低阈值',
  moraleOk: '士气正常阈值',
  securityCritical: '治安危急阈值',
  securityLow: '治安偏低阈值',
  securityOk: '治安正常阈值',
  weaponCoverRatio: '武器覆盖率',
  weaponCrisisTroops: '武器紧缺兵力线',
  crisisFoodFill: '缺粮紧急填充',
  rewardGoldThreshold: '赏赐资金阈值',
  researchGoldThreshold: '研究资金阈值',
  machineEnough: '兵器充足判据',

  // AI 参数：评分
  scoreAttackBorderBonus: '评分 · 攻击边境加成',
  scoreAttackTroopHighBonus: '评分 · 兵力充足加成',
  scoreAttackTroopLowPenalty: '评分 · 兵力不足惩罚',
  scoreAttackNoPersonPenalty: '评分 · 无将惩罚',
  scoreAttackSiegePenalty: '评分 · 围城惩罚',
  scoreReinforcePerNeighbor: '评分 · 每邻接增援',
  scoreReinforceTroopBonus: '评分 · 增援兵力加成',
  scoreReinforceSelfSiegePenalty: '评分 · 本城被围惩罚',
  scoreFoodCriticalBonus: '评分 · 兵粮危急加成',
  scoreFoodLowBonus: '评分 · 兵粮偏低加成',
  scoreFoodDefaultBonus: '评分 · 兵粮常规加成',
  scoreInternalDefault: '评分 · 内政常规',
  scoreInternalSiegePenalty: '评分 · 内政围城惩罚',
  scoreInternalBorderPenalty: '评分 · 内政边境惩罚',
  scoreInternalRichBonus: '评分 · 内政富裕加成',
  scoreSecurityCriticalBonus: '评分 · 治安危急加成',
  scoreSecurityLowBonus: '评分 · 治安偏低加成',
  scoreMoraleLowBonus: '评分 · 士气偏低加成',
  scoreMoraleOkBonus: '评分 · 士气正常加成',
  scoreMoraleDefaultBonus: '评分 · 士气常规加成',
  scoreRewardBase: '评分 · 赏赐基数',
  scoreRewardGoldBonus: '评分 · 赏赐资金加成',
  scoreRecruitTroopCriticalBonus: '评分 · 征兵危急加成',
  scoreRecruitTroopLowBonus: '评分 · 征兵偏低加成',
  scoreRecruitSecurityPenalty: '评分 · 征兵治安惩罚',
  scoreWeaponCriticalBonus: '评分 · 武器危急加成',
  scoreWeaponLowBonus: '评分 · 武器偏低加成',
  scoreMachineEnoughPenalty: '评分 · 兵器充足惩罚',
  scoreMachineBonus: '评分 · 兵器加成',
  scoreBoatPortBonus: '评分 · 港口舰船加成',
  scoreBoatPenalty: '评分 · 舰船惩罚',
  scoreSearchingNoPersonPenalty: '评分 · 无将搜索惩罚',
  scoreSearchingBonus: '评分 · 搜索加成',
  scoreRecruitPersonNoPersonPenalty: '评分 · 无人可募惩罚',
  scoreRecruitPersonBonus: '评分 · 招募人才加成',
  scoreTransfromRichBonus: '评分 · 富裕输送加成',
  scoreTransfromDefaultBonus: '评分 · 输送常规加成',
  scoreTransfromToBelongCityBonus: '评分 · 输送回本城加成',
  scoreResearchSiegePenalty: '评分 · 研究围城惩罚',
  scoreResearchGoldBonus: '评分 · 研究资金加成',
  scoreResearchDefaultBonus: '评分 · 研究常规加成',
  scoreSupplyBonus: '评分 · 补给加成',
  scoreSupplyPenalty: '评分 · 补给惩罚',

  // AI 参数：个性与倾向
  personalityAggressiveMilitaryBonus: '个性 · 好战军事加成',
  personalityAggressiveInternalPenalty: '个性 · 好战内政惩罚',
  personalityDefensiveMilitaryBonus: '个性 · 稳重军事加成',
  personalityDefensiveInternalBonus: '个性 · 稳重内政加成',
  personalityEconomicInternalBonus: '个性 · 经济内政加成',
  personalityEconomicMilitaryPenalty: '个性 · 经济军事惩罚',
  personalityDiplomaticPersonBonus: '个性 · 外交人才加成',
  siegeDefenseBias: '围城防守偏向',
  siegeInternalPenalty: '围城内政惩罚',
  reinforceBias: '增援偏向',
  threatBias: '威胁偏向',
  peacetimeInternalBias: '和平期内政偏向',
  peaceInternalBias: '和平期内政偏向',
  peaceMilitaryPenalty: '和平期军事惩罚',
  crisisFoodBias: '缺粮偏向',
  crisisSecurityBias: '治安危机偏向',
  crisisWeaponBias: '武器危机偏向',
  borderRingBonus: '边境环加成',
  subCityBias: '属城偏向',
  subFrontRingBonus: '属城前线环加成',
  missionFocusScale: '任务聚焦系数',
  retreatChance: '撤退几率',
  counterPenaltyScale: '反击惩罚系数',
  recommendedTeamBonus: '推荐队伍加成',
  frontwardCostFactor: '向前移动代价系数',
  backwardCostFactor: '向后移动代价系数',
  crossRingCost: '跨环代价',
  enhanceDynamicRearFloor: '启用动态后方法下限',
  enableDynamicRearFloor: '启用动态后方法下限',
  enableIncrementalPlan: '启用增量计划',
  enablePortGateDistribute: '启用关港分配',
  enforceCorpsBoundary: '强制军团边界',
  turnDays: '每回合天数',
  shadowLogEnabled: '启用影子日志',
  shadowLogIntervalTurns: '影子日志间隔回合',
  shadowOnly: '仅记录影子',
  debounceTurns: '去抖回合数',
  earlyGameTurns: '前期回合数',
}

/**
 * 取字段的中文标签。
 *
 * @param key 字段键
 * @param fileMeta 文件元数据（可提供文件级覆盖）
 * @returns 标签；未命中返回原始键名
 */
export function commonFieldLabel(key: string, fileMeta?: CommonFileMeta): string {
  const override = (fileMeta as CommonFileMeta & { fieldLabels?: Record<string, string> })?.fieldLabels
  if (override && override[key]) return override[key]
  return COMMON_FIELD_LABELS[key] ?? key
}

/* ------------------------------------------------------------------ */
/* 引用关系字典                                                        */
/* ------------------------------------------------------------------ */

/**
 * 各文件字段的引用关系。
 *
 * key 为文件名，value 为「字段名 -> 引用目标」。
 * 未登记的字段一律按普通值编辑。
 */
export const COMMON_FIELD_REFS: Record<string, Record<string, RefSpec>> = {
  'PersonLibrary.json': {
    BelongForce: scn('forceSet'),
    BelongCorps: scn('corpsSet'),
    BelongCity: scn('citySet'),
    Father: scn('personSet'),
    Mother: scn('personSet'),
    Brother: scn('personSet'),
    Official: opt('officials'),
    Level: opt('personLevels'),
    personality: opt('personalities'),
    argumentation: opt('argumentations'),
  },
  'duelPersonBehaviour.json': { personId: scn('personSet') },
  'debatePersonBehaviour.json': { personId: scn('personSet'), featureId: opt('features') },
  'AIConfig.json': { shadowLogForceId: scn('forceSet'), shadowLogCorpsId: scn('corpsSet') },
  'BuildingTypes.json': {
    needTech: opt('techniques'),
    nextId: opt('buildingTypes'),
    productItems: opt('itemTypes'),
    jobId: opt('jobTypes'),
    // C# 中 jobId 是内政指令，effectAttrType 是 Person.GetAttribute 的属性下标（0~4）
    effectAttrType: st(ATTRIBUTE_ENTRIES),
  },
  'ItemTypes.json': {
    storeKind: opt('storeKinds'),
    nextId: opt('itemTypes'),
    createBuildingKind: opt('buildingTypes'),
    validTechId: opt('techniques'),
  },
  'JobTypes.json': { linkBuildingKind: opt('buildingTypes'), recommandFeatures: opt('features') },
  'Officials.json': { level: opt('personLevels'), addSkills: opt('skills'), addFeatures: opt('features') },
  'Skills.json': { needAblilityLevel: opt('abilityLevelTypes') },
  'Provinces.json': { Region: opt('regions'), neighbors: opt('provinces') },
  // needAttr 是能力下标（0统率/1武力/2智力/3政治/4魅力），不是 AttributeChangeTypes 的 ID
  'Techniques.json': { needTech: opt('techniques'), needAttr: st(ATTRIBUTE_ENTRIES) },
  'TroopTypes.json': {
    skills: opt('skills'),
    matchFeatures: opt('features'),
    validItemId: opt('itemTypes'),
    validTechId: opt('techniques'),
    // influenceAbility 是能力下标；moveCost 是按 TerrainType.Id 逐项索引的数组
    influenceAbility: st(ATTRIBUTE_ENTRIES),
    moveCost: idx(opt('terrainTypes')),
  },
  'CityLevelTypes.json': {},
}

/**
 * 容易被误判的字段说明。
 *
 * 这些字段在 JSON 里存在，但对应的 C# 类没有该成员（会落进 JsonExtensionData 且无人读取），
 * 或者语义与名称不符。展示时给出提示，避免编辑者误以为改了就生效。
 */
export const COMMON_FIELD_NOTES: Record<string, Record<string, string>> = {
  'PersonLibrary.json': {
    horse: 'C# 当前不读取该键（旧代号遗留）。装备请写 EquippedHorse / EquippedWeapon / EquippedArmor',
    left_weapon: 'C# 当前不读取该键。当前只有单一武器槽 EquippedWeapon',
    right_weapon: 'C# 当前不读取该键。当前只有单一武器槽 EquippedWeapon',
  },
  'JobTypes.json': { linkBuildingKind: 'C# 的 JobType 类没有该成员，当前不会被读取' },
  'ItemTypes.json': { subKind: 'C# 的 ItemType 类没有该成员，当前不会被读取' },
}

/**
 * 取某文件某字段的引用关系。
 *
 * @param fileName 文件名
 * @param key 字段名
 * @returns 引用目标；无引用返回 null
 */
export function commonFieldRef(fileName: string, key: string): RefSpec | null {
  return COMMON_FIELD_REFS[fileName]?.[key] ?? null
}

/**
 * 取某文件某字段的注意事项。
 *
 * @param fileName 文件名
 * @param key 字段名
 * @returns 提示文本；无提示返回 null
 */
export function commonFieldNote(fileName: string, key: string): string | null {
  return COMMON_FIELD_NOTES[fileName]?.[key] ?? null
}

/**
 * 数组元素是引用的字段（值列表需要按引用显示名称）。
 *
 * 带 indexed 的项表示「按被引用表逐项索引」，例如 TroopTypes.moveCost 的第 i 项
 * 对应 TerrainType i，显示为「地形名: 值」而不是把值当 ID 去查名称。
 */
export const COMMON_ARRAY_ITEM_REFS: Record<string, Record<string, RefSpec>> = {
  'Provinces.json': { neighbors: opt('provinces') },
  'TroopTypes.json': {
    skills: opt('skills'),
    matchFeatures: opt('features'),
    aniIds: opt('troopAnimations'),
    moveCost: idx(opt('terrainTypes')),
  },
  'PersonLibrary.json': {
    FeatureList: opt('features'),
    LikePersonList: scn('personSet'),
    HatePersonList: scn('personSet'),
    SpouseList: scn('personSet'),
    BrotherList: scn('personSet'),
  },
}

/** 分区级的提醒（按「文件名:分区键」索引） */
export const COMMON_SECTION_NOTES: Record<string, string[]> = {
  'ai.json:ai': [
    '该分区是死数据：顶层键 ai 不对应 ScenarioCommonData 的任何属性，游戏加载时会被直接丢弃，改这里不会生效。',
    '同文件的 Officials 分区会与 Officials.json 一起写入同一张 Officials 表，两者冲突时以加载顺序靠后的为准。',
  ],
  'ai.json:Officials': [
    '与 Officials.json 写入同一张表，重复定义时以加载顺序靠后的为准，建议只在其中一个文件里维护。',
  ],
  'duelPersonBehaviour.json:behaviours': [
    '由 DuelPersonBehaviourDataSet 加载，personId 是真实武将 Id（常用于自定义武将）。',
  ],
  'debatePersonBehaviour.json:behaviours': [
    'personId 与 featureId 二选一命中，前者指武将 Id，后者指特技 Id。',
  ],
  'RecommendedTroopTeams.json:teams': [
    'memberPersonIds 指武将 Id，featureIds 指特技 Id，troopTypeId 指兵种 Id。',
  ],
}
