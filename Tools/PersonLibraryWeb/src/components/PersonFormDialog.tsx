/**
 * 文件名：PersonFormDialog.tsx
 * 描述：新建 / 编辑武将的弹窗。
 *       按照字段用途划分多个分区（基本资料、容貌立绘、生卒登场、能力数值、
 *       成长状态、兵种适性、性格倾向、人际关系、特性特技、归属、其它），
 *       不同分区使用最合适的编辑控件。
 *       - 字段是否渲染由所在库的字段全集（fieldKeys）决定，
 *         因此基础武将库与自建武将库会呈现不同的编辑项；
 *       - ID 由服务端固定，编辑时不可修改（基础库 ID 与游戏本体绑定）。
 */

import { useEffect, useMemo, useState } from 'react'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  FieldShell,
  IntArrayField,
  NumberField,
  PillsField,
  SectionTitle,
  SelectField,
  SliderField,
  SwitchField,
  TagsField,
  TextAreaField,
  TextField,
} from '@/components/form-fields'
import { HeadIconPicker } from '@/components/HeadIconPicker'
import { CustomFaceDialog } from '@/components/CustomFaceDialog'
import {
  PersonRefMultiPicker,
  PersonRefPicker,
  type PersonOption,
} from '@/components/PersonRefPicker'
import { FeaturePicker } from '@/components/FeaturePicker'
import {
  ABILITY_FIELDS,
  DEFAULT_ATTRIBUTE_CHANGE_ID,
  EXTENDED_FIELD_LABELS,
  IDEAL_CHOICES,
  KANSHITSU_CHOICES,
  PERSON_TYPE_CHOICES,
  SEX_CHOICES,
  STATE_CHOICES,
  TALENT_CHOICES,
  TONE_CHOICES,
  VOICE_CHOICES,
  WEAPON_FIELDS,
  createDefaultPerson,
  formatUpdatedAt,
  labelOf,
  makeUniqueName,
  makeWeaponValue,
  statBaseValue,
  statChangeIdOf,
  withAbilityBase,
  withAbilityChangeId,
} from '@/lib/enums'
import { createPerson, updatePerson } from '@/lib/api'
import { cn } from '@/lib/utils'
import type {
  CustomFaceResult,
  LibraryCapabilities,
  LibraryKey,
  OptionsConfig,
  Person,
} from '@/lib/types'
import {
  AlertCircle,
  CalendarDays,
  Flag,
  Heart,
  ImageIcon,
  Layers,
  Loader2,
  Settings2,
  Shield,
  Sparkles,
  Swords,
  TrendingUp,
  UserRound,
  Users,
} from 'lucide-react'

/** 表单分区定义 */
const SECTIONS = [
  { key: 'basic', label: '基本资料', desc: '类型 / 姓名 / 性别 / 列传', icon: UserRound },
  { key: 'face', label: '容貌立绘', desc: '头像与立绘资源', icon: ImageIcon },
  { key: 'life', label: '生卒登场', desc: '出生 / 死亡 / 登场年', icon: CalendarDays },
  { key: 'ability', label: '能力数值', desc: '统御 武力 智力 政治 魅力', icon: Swords },
  { key: 'growth', label: '状态数值', desc: '身份 / 等级 / 经验 / 功绩', icon: TrendingUp },
  { key: 'weapon', label: '兵种适性', desc: '矛 戟 弓弩 骑 水军 器械', icon: Shield },
  { key: 'character', label: '性格倾向', desc: '性格 / 义理 / 音声 / 语气 / 理想 / 才干', icon: Heart },
  { key: 'relation', label: '人际关系', desc: '父母 / 配偶 / 兄弟 / 好恶', icon: Users },
  { key: 'feature', label: '特性特技', desc: '武将特性组合', icon: Sparkles },
  { key: 'belong', label: '归属忠诚', desc: '势力 / 军团 / 城市 / 官职 / 忠诚', icon: Flag },
  { key: 'extended', label: '本体扩展', desc: '新版本体数据特有字段', icon: Layers },
  { key: 'misc', label: '其它', desc: '相性 / 血缘 / 状态', icon: Settings2 },
] as const

type SectionKey = (typeof SECTIONS)[number]['key']

/**
 * 各分区包含的字段名。
 * 分区内所有字段都不在当前库时，该分区不会出现在导航中。
 */
const SECTION_FIELDS: Record<SectionKey, string[]> = {
  basic: ['type', 'familyName', 'giveName', 'nickName', 'sex', 'description', 'tags'],
  face: ['headIconID', 'image', 'imageID', 'image_old'],
  life: ['yearBorn', 'yearDead', 'appearance'],
  ability: ['command', 'strength', 'intelligence', 'politics', 'glamour'],
  growth: ['state', 'Level', 'Exp', 'merit', 'stamina'],
  weapon: ['spearLv', 'halberdLv', 'crossbowLv', 'rideLv', 'waterLv', 'machineLv'],
  character: ['personality', 'argumentation', 'voice', 'tone', 'kanshitsu', 'ideal', 'talent'],
  relation: [
    'Father',
    'Mother',
    'consanguinity',
    'SpouseList',
    'Brother',
    'BrotherList',
    'LikePersonList',
    'HatePersonList',
  ],
  feature: ['FeatureList'],
  belong: ['BelongForce', 'BelongCorps', 'BelongCity', 'Official', 'loyalty'],
  // 新版本体数据特有字段（旧版数据不含这些字段，因此该分区只在有数据时出现）
  extended: [
    'skeleton',
    'body',
    'birthplace',
    'generation',
    'ambition',
    'strategic_tendency',
    'hanLoyalty',
    'local_affiliation',
    'promotion',
    'death_type',
    'ketsuen',
    'horse',
    'left_weapon',
    'right_weapon',
    'injury',
    'wadai',
    'wajutsu',
    'wordTac',
    'CurrentCity',
    'oldAge',
    'old_age',
  ],
  misc: ['compatibility', 'IsAlive', 'ActionOver'],
}

interface PersonFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** 编辑中的武将；为空表示新建 */
  initial: Person | null
  /** 当前库中全部武将（用于同名校验与人际关系候选） */
  persons: Person[]
  /** 人际关系候选列表 */
  personOptions: PersonOption[]
  /** 武将姓名索引（Id -> 姓名） */
  nameIndex: Record<number, string>
  /** 编辑选项配置 */
  options: OptionsConfig
  /** 当前库标识 */
  lib: LibraryKey
  /** 当前库允许的操作 */
  capabilities: LibraryCapabilities
  /** 当前库武将的字段全集 */
  fieldKeys: string[]
  /** 已占用的姓名（基础武将库全部姓名，用于自建库跨库查重） */
  reservedNames: string[]
  /** 是否只读（游客只读浏览时禁用全部输入并隐藏保存按钮） */
  readOnly?: boolean
  /** 服务端的自制头像数据（供头像选择器展示与制作） */
  customFaces?: CustomFaceResult | null
  /** 自制头像新增后刷新列表 */
  onCustomFacesChanged?: () => void
  /** 需要登录时的回调（登录态失效等场景） */
  onRequireLogin?: () => void
  /** 保存成功回调，回传保存后的武将 */
  onSaved: (person: Person) => void
}

export function PersonFormDialog({
  open,
  onOpenChange,
  initial,
  persons,
  personOptions,
  nameIndex,
  options,
  lib,
  capabilities,
  fieldKeys,
  reservedNames,
  readOnly = false,
  customFaces = null,
  onCustomFacesChanged,
  onRequireLogin,
  onSaved,
}: PersonFormDialogProps) {
  const isEdit = Boolean(initial && initial.Id > 0)
  const [form, setForm] = useState<Person>(createDefaultPerson())
  const [active, setActive] = useState<SectionKey>('basic')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  /** 自制头像制作弹窗 */
  const [customFaceOpen, setCustomFaceOpen] = useState(false)

  /** 当前库的字段集合，用于决定渲染哪些字段 */
  const visibleKeys = useMemo(() => new Set(fieldKeys), [fieldKeys])

  /** 判断字段是否属于当前库 */
  const has = (key: string) => visibleKeys.has(key)

  /** 需要展示的分区（分区内至少存在一个字段） */
  const sections = useMemo(
    () => SECTIONS.filter((s) => SECTION_FIELDS[s.key].some((f) => visibleKeys.has(f))),
    [visibleKeys],
  )

  /** 库中已使用的标签（按使用次数降序，供标签编辑快捷点选，避免同义标签写法不一致） */
  const knownTags = useMemo(() => {
    const counter = new Map<string, number>()
    for (const p of persons) {
      for (const tag of p.tags ?? []) counter.set(tag, (counter.get(tag) ?? 0) + 1)
    }
    return [...counter.entries()]
      .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0], 'zh-Hans-CN'))
      .map(([tag]) => tag)
  }, [persons])

  // 弹窗打开时初始化表单数据
  useEffect(() => {
    if (!open) return
    setActive('basic')
    setError('')
    if (initial && initial.Id > 0) {
      // 基础武将库保留原始字段集合（避免写入本体不存在的字段）
      setForm(lib === 'base' ? { ...initial } : { ...createDefaultPerson(), ...initial })
    } else {
      setForm(createDefaultPerson())
    }
  }, [open, initial, lib])

  /** 通用字段写入 */
  const set = <K extends keyof Person>(key: K, value: Person[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }))
  }

  /**
   * 动态字段写入（按字段名渲染的扩展字段使用）。
   * 新版本体数据的字段类型与 Person 类型定义不完全一致（如能力为数组），
   * 因此这里以 string 键 + unknown 值写入并断言为 Person。
   * @param key 字段名
   * @param value 字段值
   */
  const setAny = (key: string, value: unknown) => {
    setForm((prev) => ({ ...prev, [key]: value }) as Person)
  }

  /**
   * 取数值字段的基础值（缺省为 0）。
   * 新版本体数据把能力 / 适性存为数组 [基础值, 成长类型Id]，这里统一取首元素。
   */
  const num = (key: keyof Person): number => statBaseValue(form[key])

  /** 格式化成 0 或数组 */
  const asArray = (v: number[] | null): number[] | null => (v && v.length ? v : null)

  /** 能力合计 */
  const abilityTotal = useMemo(
    () => ABILITY_FIELDS.reduce((sum, f) => sum + num(f.key), 0),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [form],
  )

  /** 适性等级选项 */
  const levelChoices = useMemo(
    () =>
      options.abilityLevels.length
        ? options.abilityLevels.map((l) => ({ value: l.id, label: l.name }))
        : [
            { value: 0, label: 'C' },
            { value: 1, label: 'B' },
            { value: 2, label: 'A' },
            { value: 3, label: 'S' },
          ],
    [options.abilityLevels],
  )

  /**
   * 能力成长类型选项（AttributeChangeType：1 超持续型 / 2 持续型 / 3 早熟型 /
   * 4 早熟持续型 / 5 普通型 / 6 普通持续型 / 7 晚成型 / 8 超晚成型 / 9 张飞型）。
   * 选项由后端 options.json 下发；0 表示未设置，游戏内按 5 普通型处理。
   */
  const changeTypeChoices = useMemo(
    () => [
      { value: 0, label: '（未设置·按普通型）' },
      ...(options.attributeChangeTypes ?? []).map((c) => ({ value: c.id, label: c.name })),
    ],
    [options.attributeChangeTypes],
  )

  /** 武将类型选项（优先使用后端下发，保证与 PersonTypeEnum 一致） */
  const typeChoices = useMemo(
    () =>
      options.personTypes && options.personTypes.length
        ? options.personTypes.map((t) => ({ value: t.id, label: t.name }))
        : PERSON_TYPE_CHOICES,
    [options.personTypes],
  )

  /** 完整姓名 */
  const fullName = `${form.familyName}${form.giveName}`.trim()

  /**
   * 实际会写入的姓名。
   * 基础武将库中存在 Name 与“姓 + 名”写法不一致的异体字情况，
   * 因此未改动姓 / 名时沿用库中已有的 Name。
   */
  const finalName = useMemo(() => {
    if (lib === 'base' && initial && initial.Id > 0) {
      const famSame = (form.familyName ?? '') === (initial.familyName ?? '')
      const giveSame = (form.giveName ?? '') === (initial.giveName ?? '')
      if (famSame && giveSame) return initial.Name || fullName
    }
    return fullName
  }, [lib, initial, form.familyName, form.giveName, fullName])

  /**
   * 已占用的姓名集合。
   * 自建库需与「基础武将库姓名 + 本库其它武将姓名」查重（排除自身）。
   */
  const usedNames = useMemo(() => {
    const set = new Set<string>()
    for (const n of reservedNames) {
      const t = (n ?? '').trim()
      if (t) set.add(t)
    }
    for (const p of persons) {
      if (p.Id === form.Id) continue
      const n = (p.Name || `${p.familyName}${p.giveName}`).trim()
      if (n) set.add(n)
    }
    return set
  }, [reservedNames, persons, form.Id])

  /** 最终写入的姓名：自建库冲突时自动追加 #1 后缀（基础库保持原名） */
  const previewName = useMemo(
    () => (lib === 'custom' ? makeUniqueName(finalName, usedNames) : finalName),
    [lib, finalName, usedNames],
  )

  /** 是否因重名被自动改名 */
  const willRename = lib === 'custom' && Boolean(finalName) && previewName !== finalName

  /** 保存 */
  const handleSave = async () => {
    setError('')
    if (!finalName) {
      setError('请至少填写「姓」或「名」')
      setActive('basic')
      return
    }

    setSaving(true)
    try {
      // 自建库提交去重后的姓名（与基础库及本库查重），服务端会再次确认
      const payload: Partial<Person> = { ...form, Name: previewName || finalName }
      const saved = isEdit
        ? await updatePerson(form.Id, payload, lib)
        : await createPerson(payload, lib)
      onSaved(saved.person)
      onOpenChange(false)
    } catch (err) {
      setError(err instanceof Error ? err.message : '保存失败')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex h-[86vh] max-w-5xl flex-col gap-0 p-0 sm:max-w-5xl">
        <DialogHeader className="border-b border-border px-6 py-4">
          <DialogTitle className="flex items-center gap-3 font-display text-xl">
            {readOnly ? '查看武将' : isEdit ? '编辑武将' : '新建武将'}
            <Badge variant="secondary" className="font-normal">
              {labelOf(PERSON_TYPE_CHOICES, num('type'))}
            </Badge>
            {readOnly && (
              <Badge variant="outline" className="font-normal">
                只读
              </Badge>
            )}
            {isEdit && (
              <Badge variant="outline" className="font-normal">
                ID #{form.Id}
                {capabilities.readOnlyId && '（不可修改）'}
              </Badge>
            )}
            {lib === 'custom' && form.updatedAt && (
              <span className="text-xs font-normal text-muted-foreground">
                修改于 {formatUpdatedAt(form.updatedAt)}
              </span>
            )}
          </DialogTitle>
          <DialogDescription>
            {readOnly
              ? '当前身份为游客，仅可浏览武将资料。登录后即可修改。'
              : lib === 'base'
                ? '基础武将库：ID 与游戏本体绑定，不可修改、不可删除；其它字段均可修改。'
                : isEdit
                  ? '武将 ID 固定不可修改；姓名与基础库及本库其它武将冲突时将自动追加 #1 后缀。'
                  : 'ID 由系统按起始编号自动分配；姓名与基础库及本库其它武将冲突时将自动追加 #1 后缀。'}
          </DialogDescription>
        </DialogHeader>

        <div className="flex min-h-0 flex-1">
          {/*
            左侧分区导航。
            分区较多（12 项），弹窗高度受视口限制时按钮会溢出行高且没有滚动条，
            导致最下方的分区（如「其它」）无法点选；这里用可滚动容器包裹，
            并让按钮 shrink-0 避免被压缩成看不清的高度。
          */}
          <ScrollArea className="hidden w-48 shrink-0 border-r border-border md:block">
            <nav className="flex flex-col gap-1 p-3">
              {sections.map((s) => {
                const Icon = s.icon
                return (
                  <button
                    key={s.key}
                    type="button"
                    onClick={() => setActive(s.key)}
                    className={cn(
                      'flex shrink-0 cursor-pointer items-center gap-3 rounded-md px-3 py-2 text-left transition-colors duration-150',
                      active === s.key
                        ? 'bg-primary/15 text-primary'
                        : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                    )}
                  >
                    <Icon className="size-4 shrink-0" />
                    <span className="flex flex-col">
                      <span className="text-sm font-medium">{s.label}</span>
                      <span className="text-[10px] text-muted-foreground">{s.desc}</span>
                    </span>
                  </button>
                )
              })}
            </nav>
          </ScrollArea>

          {/* 右侧内容 */}
          <ScrollArea className="min-h-0 flex-1">
            {/* 只读模式（游客）下由 fieldset 统一禁用全部表单控件 */}
            <fieldset disabled={readOnly} className="m-0 min-w-0 border-0 p-6">
              {/* 移动端分区选择 */}
              <div className="mb-4 flex flex-wrap gap-1.5 md:hidden">
                {sections.map((s) => (
                  <button
                    key={s.key}
                    type="button"
                    onClick={() => setActive(s.key)}
                    className={cn(
                      'cursor-pointer rounded-md border px-2.5 py-1 text-xs transition-colors',
                      active === s.key
                        ? 'border-primary bg-primary/15 text-primary'
                        : 'border-border text-muted-foreground',
                    )}
                  >
                    {s.label}
                  </button>
                ))}
              </div>

              {active === 'basic' && (
                <div className="flex flex-col gap-5">
                  <SectionTitle title="基本资料" description="武将类型、姓名、性别与人物列传" />
                  <PillsField
                    label="武将类型"
                    hint="type（PersonTypeEnum）"
                    value={num('type')}
                    choices={typeChoices}
                    onChange={(v) => set('type', v)}
                  />
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                    <TextField
                      label="姓"
                      hint="familyName"
                      value={form.familyName}
                      placeholder="例如：赵"
                      onChange={(v) => set('familyName', v)}
                    />
                    <TextField
                      label="名"
                      hint="giveName"
                      value={form.giveName}
                      placeholder="例如：云"
                      onChange={(v) => set('giveName', v)}
                    />
                    {has('nickName') && (
                      <TextField
                        label="字"
                        hint="nickName"
                        value={form.nickName ?? ''}
                        placeholder="例如：子龙"
                        onChange={(v) => set('nickName', v)}
                      />
                    )}
                  </div>
                  {has('sex') && (
                    <PillsField
                      label="性别"
                      hint="影响容貌区间与音声合法性"
                      value={num('sex')}
                      choices={SEX_CHOICES}
                      onChange={(v) => {
                        set('sex', v)
                        // 性别切换后自动切换到对应容貌区间
                        const range = options.headRanges.find((r) => r.sexType === v)
                        if (range && (num('headIconID') < range.startId || num('headIconID') > range.endId)) {
                          set('headIconID', range.startId)
                        }
                      }}
                    />
                  )}
                  <FieldShell
                    label="姓名预览"
                    hint={
                      capabilities.enforceUniqueName
                        ? '由姓 + 名组合；与基础库及本库其它武将重名时将自动追加 #1 后缀'
                        : '基础武将库保留原有姓名，改动姓 / 名后才会重算'
                    }
                  >
                    <div className="flex flex-col gap-2">
                      <div className="flex min-h-9 items-center gap-2 rounded-md border border-border bg-secondary/40 px-3 text-sm">
                        {previewName || <span className="text-muted-foreground">（未填写）</span>}
                        {willRename && (
                          <Badge variant="secondary" className="font-normal">
                            原名「{finalName}」已重名
                          </Badge>
                        )}
                      </div>
                      {willRename && (
                        <p className="text-xs text-muted-foreground">
                          该姓名与基础武将库或本库其它武将冲突，保存后将自动命名为「
                          <span className="text-primary">{previewName}</span>」。
                        </p>
                      )}
                    </div>
                  </FieldShell>
                  {has('description') &&
                    (typeof form.description === 'number' ? (
                      <NumberField
                        label="列传编号"
                        hint="description（基础库为该字段的编号）"
                        value={form.description}
                        min={0}
                        onChange={(v) => set('description', v)}
                      />
                    ) : (
                      <TextAreaField
                        label="列传 / 生平"
                        hint="description"
                        value={String(form.description ?? '')}
                        placeholder="输入武将的生平介绍..."
                        rows={6}
                        onChange={(v) => set('description', v)}
                      />
                    ))}
                  {lib === 'custom' && has('tags') && (
                    <TagsField
                      label="标签"
                      hint="tags（仅自建武将库，用于分类与列表筛选）"
                      value={form.tags ?? []}
                      suggestions={knownTags}
                      disabled={readOnly}
                      onChange={(v) => set('tags', v)}
                    />
                  )}
                </div>
              )}

              {active === 'face' && (
                <div className="flex flex-col gap-5">
                  <SectionTitle title="容貌立绘" description="头像 ID 决定游戏中显示的头像" />
                  {has('headIconID') && (
                    <FieldShell label="头像" hint="headIconID">
                      <HeadIconPicker
                        value={num('headIconID')}
                        sex={num('sex')}
                        ranges={options.headRanges}
                        customFaces={customFaces}
                        canCreateCustom={!readOnly}
                        onChange={(v) => set('headIconID', v)}
                        onCreateCustom={() => setCustomFaceOpen(true)}
                      />
                    </FieldShell>
                  )}
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                    {has('image') && (
                      <TextField
                        label="立绘"
                        hint="image"
                        value={form.image ?? ''}
                        placeholder="例如：3/Zhaoyun.png"
                        onChange={(v) => set('image', v)}
                      />
                    )}
                    {has('imageID') &&
                      (typeof form.imageID === 'number' ? (
                        <NumberField
                          label="立绘 ID（弃用）"
                          hint="imageID"
                          value={form.imageID}
                          min={0}
                          onChange={(v) => set('imageID', v)}
                        />
                      ) : (
                        <TextField
                          label="立绘 ID（弃用）"
                          hint="imageID"
                          value={(form.imageID as string) ?? ''}
                          onChange={(v) => set('imageID', v || null)}
                        />
                      ))}
                    {has('image_old') && (
                      <TextField
                        label="旧立绘字段"
                        hint="image_old"
                        value={form.image_old ?? ''}
                        onChange={(v) => set('image_old', v || null)}
                      />
                    )}
                  </div>
                </div>
              )}

              {active === 'life' && (
                <div className="flex flex-col gap-5">
                  <SectionTitle title="生卒与登场" description="影响武将出现的年份区间" />
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                    <NumberField
                      label="出生年"
                      hint="yearBorn"
                      value={num('yearBorn')}
                      min={0}
                      max={400}
                      onChange={(v) => set('yearBorn', v)}
                    />
                    <NumberField
                      label="死亡年"
                      hint="yearDead"
                      value={num('yearDead')}
                      min={0}
                      max={400}
                      onChange={(v) => set('yearDead', v)}
                    />
                    <NumberField
                      label="登场年"
                      hint="appearance"
                      value={num('appearance')}
                      min={0}
                      max={400}
                      onChange={(v) => set('appearance', v)}
                    />
                  </div>
                  <p className="text-xs text-muted-foreground">
                    寿命：{Math.max(0, num('yearDead') - num('yearBorn'))} 年
                  </p>
                </div>
              )}

              {active === 'ability' && (
                <div className="flex flex-col gap-5">
                  <SectionTitle
                    title="能力数值"
                    description="五项基础能力，取值 1 - 100；每项存为 [基础值, 成长类型, 经验, 万分比, 最终值]"
                  />
                  <div className="flex flex-col gap-5">
                    {ABILITY_FIELDS.map((f) => (
                      <div key={f.key} className="flex flex-col gap-2">
                        <SliderField
                          label={f.label}
                          accent
                          value={num(f.key)}
                          onChange={(v) => setAny(f.key, withAbilityBase(form[f.key], v))}
                        />
                        <SelectField
                          label={`${f.label}成长类型`}
                          hint={`${f.key}[1] · AttributeChangeType`}
                          value={statChangeIdOf(form[f.key]) ?? DEFAULT_ATTRIBUTE_CHANGE_ID}
                          choices={changeTypeChoices}
                          onChange={(v) => setAny(f.key, withAbilityChangeId(form[f.key], v))}
                        />
                      </div>
                    ))}
                  </div>
                  <div className="flex items-center justify-between rounded-md border border-border bg-secondary/30 px-4 py-3">
                    <span className="text-sm text-muted-foreground">能力合计</span>
                    <span className="font-display text-2xl text-gold">{abilityTotal}</span>
                  </div>
                </div>
              )}

              {active === 'growth' && (
                <div className="flex flex-col gap-5">
                  <SectionTitle
                    title="状态与数值"
                    description="身份、等级、经验与功绩等；能力成长类型已并入各能力的数组"
                  />
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    {has('state') && (
                      <SelectField
                        label="身份"
                        hint="state"
                        value={num('state')}
                        choices={STATE_CHOICES}
                        onChange={(v) => set('state', v)}
                      />
                    )}
                    {has('Level') && (
                      <NumberField
                        label="等级"
                        hint="Level"
                        value={num('Level')}
                        min={0}
                        onChange={(v) => set('Level', v)}
                      />
                    )}
                    {has('Exp') && (
                      <NumberField
                        label="经验"
                        hint="Exp"
                        value={num('Exp')}
                        min={0}
                        onChange={(v) => set('Exp', v)}
                      />
                    )}
                    {has('merit') && (
                      <NumberField
                        label="功绩"
                        hint="merit"
                        value={num('merit')}
                        min={0}
                        onChange={(v) => set('merit', v)}
                      />
                    )}
                    {has('stamina') && (
                      <NumberField
                        label="体力"
                        hint="stamina"
                        value={num('stamina')}
                        min={0}
                        onChange={(v) => set('stamina', v)}
                      />
                    )}
                  </div>
                </div>
              )}

              {active === 'weapon' && (
                <div className="flex flex-col gap-5">
                  <SectionTitle title="兵种适性" description="各项适性等级：C / B / A / S / S1 - S4" />
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                    {WEAPON_FIELDS.filter((f) => has(f.key)).map((f) => (
                      <SelectField
                        key={f.key}
                        label={f.label}
                        hint={f.key}
                        value={num(f.key)}
                        choices={levelChoices}
                        // 适性存为 [基础值(等级), 经验, 等级]，改写后归一化为完整 3 元组
                        onChange={(v) => setAny(f.key, makeWeaponValue(v))}
                      />
                    ))}
                  </div>
                </div>
              )}

              {active === 'character' && (
                <div className="flex flex-col gap-6">
                  <SectionTitle title="性格倾向" description="决定武将的 AI 行为与台词表现" />
                  {has('personality') && (
                    <PillsField
                      label="性格"
                      hint="personality"
                      value={num('personality')}
                      choices={
                        options.personalities.length
                          ? options.personalities.map((c) => ({ value: c.id, label: c.name }))
                          : [
                              { value: 1, label: '胆小' },
                              { value: 2, label: '冷静' },
                              { value: 3, label: '刚胆' },
                              { value: 4, label: '莽撞' },
                            ]
                      }
                      onChange={(v) => set('personality', v)}
                    />
                  )}
                  {has('argumentation') && (
                    <SelectField
                      label="义理"
                      hint="argumentation"
                      value={num('argumentation')}
                      className="max-w-xs"
                      choices={
                        options.argumentations.length
                          ? [
                              { value: 0, label: '（未设置）' },
                              ...options.argumentations.map((c) => ({ value: c.id, label: c.name })),
                            ]
                          : [{ value: 0, label: '（未设置）' }]
                      }
                      onChange={(v) => set('argumentation', v)}
                    />
                  )}
                  {has('voice') && (
                    <PillsField
                      label="音声"
                      hint="voice"
                      value={num('voice')}
                      choices={VOICE_CHOICES}
                      onChange={(v) => set('voice', v)}
                    />
                  )}
                  {has('tone') && (
                    <PillsField
                      label="语气"
                      hint="tone"
                      value={num('tone')}
                      choices={TONE_CHOICES}
                      onChange={(v) => set('tone', v)}
                    />
                  )}
                  {has('kanshitsu') && (
                    <PillsField
                      label="汉室态度"
                      hint="kanshitsu"
                      value={num('kanshitsu')}
                      choices={KANSHITSU_CHOICES}
                      onChange={(v) => set('kanshitsu', v)}
                    />
                  )}
                  {has('ideal') && (
                    <PillsField
                      label="理想"
                      hint="ideal"
                      value={num('ideal')}
                      choices={IDEAL_CHOICES}
                      onChange={(v) => set('ideal', v)}
                    />
                  )}
                  {has('talent') && (
                    <PillsField
                      label="才干"
                      hint="talent"
                      value={num('talent')}
                      choices={TALENT_CHOICES}
                      onChange={(v) => set('talent', v)}
                    />
                  )}
                </div>
              )}

              {active === 'relation' && (
                <div className="flex flex-col gap-6">
                  <SectionTitle
                    title="人际关系"
                    description="可选择当前库武将或游戏内置武将，未设置时值为 0 / 空"
                  />
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    {has('Father') && (
                      <FieldShell label="父亲" hint="Father">
                        <PersonRefPicker
                          value={num('Father')}
                          options={personOptions}
                          onChange={(v) => set('Father', v)}
                        />
                      </FieldShell>
                    )}
                    {has('Mother') && (
                      <FieldShell label="母亲" hint="Mother">
                        <PersonRefPicker
                          value={num('Mother')}
                          options={personOptions}
                          onChange={(v) => set('Mother', v)}
                        />
                      </FieldShell>
                    )}
                    {has('consanguinity') && (
                      <FieldShell label="血缘" hint="consanguinity">
                        <Input
                          type="number"
                          min={0}
                          value={num('consanguinity')}
                          onChange={(e) => set('consanguinity', Number(e.target.value) || 0)}
                        />
                      </FieldShell>
                    )}
                    {has('Brother') && (
                      <NumberField
                        label="兄弟"
                        hint="Brother"
                        value={num('Brother')}
                        min={0}
                        onChange={(v) => set('Brother', v)}
                      />
                    )}
                  </div>
                  {has('SpouseList') && (
                    <FieldShell label="配偶" hint="SpouseList">
                      <PersonRefMultiPicker
                        value={(form.SpouseList as number[] | null) ?? null}
                        options={personOptions}
                        nameIndex={nameIndex}
                        onChange={(v) => set('SpouseList', asArray(v))}
                      />
                    </FieldShell>
                  )}
                  {has('BrotherList') && (
                    <FieldShell label="兄弟列表" hint="BrotherList">
                      <PersonRefMultiPicker
                        value={(form.BrotherList as number[] | null) ?? null}
                        options={personOptions}
                        nameIndex={nameIndex}
                        onChange={(v) => set('BrotherList', asArray(v))}
                      />
                    </FieldShell>
                  )}
                  {has('LikePersonList') && (
                    <FieldShell label="喜欢武将" hint="LikePersonList">
                      <PersonRefMultiPicker
                        value={(form.LikePersonList as number[] | null) ?? null}
                        options={personOptions}
                        nameIndex={nameIndex}
                        onChange={(v) => set('LikePersonList', asArray(v))}
                      />
                    </FieldShell>
                  )}
                  {has('HatePersonList') && (
                    <FieldShell label="厌恶武将" hint="HatePersonList">
                      <PersonRefMultiPicker
                        value={(form.HatePersonList as number[] | null) ?? null}
                        options={personOptions}
                        nameIndex={nameIndex}
                        onChange={(v) => set('HatePersonList', asArray(v))}
                      />
                    </FieldShell>
                  )}
                </div>
              )}

              {active === 'feature' && (
                <div className="flex flex-col gap-5">
                  <SectionTitle title="特性特技" description="为武将组合特性（FeatureList）" />
                  <FeaturePicker
                    value={(form.FeatureList as number[] | null) ?? null}
                    features={options.features}
                    onChange={(v) => set('FeatureList', asArray(v))}
                  />
                </div>
              )}

              {active === 'belong' && (
                <div className="flex flex-col gap-5">
                  <SectionTitle
                    title="归属与忠诚"
                    description="基础武将库专有字段，用于定义武将在剧本中的默认归属"
                  />
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <NumberField
                      label="归属势力"
                      hint="BelongForce"
                      value={num('BelongForce')}
                      min={0}
                      onChange={(v) => set('BelongForce', v)}
                    />
                    <NumberField
                      label="归属军团"
                      hint="BelongCorps"
                      value={num('BelongCorps')}
                      min={0}
                      onChange={(v) => set('BelongCorps', v)}
                    />
                    <NumberField
                      label="归属城市"
                      hint="BelongCity"
                      value={num('BelongCity')}
                      min={0}
                      onChange={(v) => set('BelongCity', v)}
                    />
                    <NumberField
                      label="官职"
                      hint="Official"
                      value={num('Official')}
                      min={0}
                      onChange={(v) => set('Official', v)}
                    />
                    <NumberField
                      label="忠诚度"
                      hint="loyalty"
                      value={num('loyalty')}
                      min={0}
                      max={100}
                      onChange={(v) => set('loyalty', v)}
                    />
                  </div>
                </div>
              )}

              {active === 'extended' && (
                <div className="flex flex-col gap-5">
                  <SectionTitle
                    title="本体扩展字段"
                    description="新版本体数据特有字段；数组字段按原长度逐项编辑（-1 表示未设置）"
                  />
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                    {EXTENDED_FIELD_LABELS.filter((f) => has(f.key)).map((f) =>
                      f.array && Array.isArray(form[f.key as keyof Person]) ? (
                        <IntArrayField
                          key={f.key}
                          label={f.label}
                          hint={f.key}
                          value={form[f.key as keyof Person] as number[]}
                          disabled={readOnly}
                          onChange={(v) => setAny(f.key, v)}
                        />
                      ) : (
                        <NumberField
                          key={f.key}
                          label={f.label}
                          hint={f.key}
                          value={num(f.key as keyof Person)}
                          onChange={(v) => setAny(f.key, v)}
                        />
                      ),
                    )}
                  </div>
                  <p className="text-xs text-muted-foreground">
                    这些字段来自新版本体数据；保存时只写入表单中的字段，其余字段原样保留。
                  </p>
                </div>
              )}

              {active === 'misc' && (
                <div className="flex flex-col gap-5">
                  <SectionTitle title="其它" description="相性与运行时状态" />
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <NumberField
                      label="相性"
                      hint="compatibility"
                      value={num('compatibility')}
                      min={0}
                      max={255}
                      onChange={(v) => set('compatibility', v)}
                    />
                  </div>
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    {has('IsAlive') && (
                      <SwitchField
                        label="存活"
                        hint="IsAlive"
                        value={Boolean(form.IsAlive)}
                        onChange={(v) => set('IsAlive', v)}
                      />
                    )}
                    {has('ActionOver') && (
                      <SwitchField
                        label="行动完毕"
                        hint="ActionOver"
                        value={Boolean(form.ActionOver)}
                        onChange={(v) => set('ActionOver', v)}
                      />
                    )}
                  </div>
                </div>
              )}
            </fieldset>
          </ScrollArea>
        </div>

        <DialogFooter className="flex-row items-center justify-between gap-3 border-t border-border px-6 py-4 sm:justify-between">
          <div className="flex min-h-5 flex-1 items-center gap-2 text-sm text-destructive">
            {error && (
              <>
                <AlertCircle className="size-4 shrink-0" />
                <span>{error}</span>
              </>
            )}
          </div>
          <div className="flex items-center gap-2">
            <Badge variant="secondary" className="hidden sm:inline-flex">
              <Layers className="size-3" />
              {finalName || '未命名'}
            </Badge>
            <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
              {readOnly ? '关闭' : '取消'}
            </Button>
            {!readOnly && (
              <Button onClick={handleSave} disabled={saving}>
                {saving && <Loader2 className="animate-spin" />}
                {isEdit ? '保存修改' : '创建武将'}
              </Button>
            )}
          </div>
        </DialogFooter>
      </DialogContent>

      {/* 自制头像制作弹窗：制作完成后自动填入当前武将的头像字段 */}
      <CustomFaceDialog
        open={customFaceOpen}
        onOpenChange={setCustomFaceOpen}
        canWrite={!readOnly}
        defaultSex={num('sex')}
        faces={customFaces}
        onCreated={(id) => {
          set('headIconID', id)
          onCustomFacesChanged?.()
        }}
        onFacesChanged={onCustomFacesChanged}
        onRequireLogin={() => onRequireLogin?.()}
      />
    </Dialog>
  )
}
