/**
 * 文件名：PersonCard.tsx
 * 描述：武将列表中的单个武将卡片。展示头像、姓名、ID、性别、身份、标签、
 *       五维能力与兵种适性，并提供编辑 / 删除入口；
 *       处于批量选择模式时，卡片左上角显示勾选框，点击卡片为勾选 / 取消勾选。
 *       头像优先展示半身像（{id}_1.png），资源缺失时回退为头像（{id}_2.png）。
 */

import { useEffect, useState } from 'react'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { cn } from '@/lib/utils'
import {
  ABILITY_FIELDS,
  PERSON_TYPE_CHOICES,
  SEX_CHOICES,
  STATE_CHOICES,
  WEAPON_FIELDS,
  abilityLevelLabel,
  formatUpdatedAt,
  headIconUrl,
  labelOf,
  statBaseValue,
} from '@/lib/enums'
import type { Person } from '@/lib/types'
import { Pencil, Trash2 } from 'lucide-react'

interface PersonCardProps {
  person: Person
  /** 是否允许编辑（游客只读，为 false 时点击卡片进入只读查看） */
  canEdit: boolean
  /** 是否允许删除（基础武将库中的武将不可删除） */
  canDelete: boolean
  /** 打开武将详情（有权限时为编辑，无权限时为只读查看） */
  onOpen: (person: Person) => void
  /** 删除 */
  onDelete: (person: Person) => void
  /** 批量选择模式：点击卡片为勾选 / 取消勾选，而不是打开详情 */
  selectable?: boolean
  /** 当前是否被勾选（仅批量选择模式下有意义） */
  selected?: boolean
  /** 勾选状态变化 */
  onToggleSelect?: (person: Person) => void
}

export function PersonCard({
  person,
  canEdit,
  canDelete,
  onOpen,
  onDelete,
  selectable = false,
  selected = false,
  onToggleSelect,
}: PersonCardProps) {
  const name = person.Name || `${person.familyName}${person.giveName}`
  const sexLabel = labelOf(SEX_CHOICES, person.sex)
  const stateLabel = labelOf(STATE_CHOICES, person.state)

  return (
    <Card
      className={cn(
        'card-hover group relative cursor-pointer overflow-hidden',
        selectable && selected && 'ring-2 ring-primary',
      )}
      onClick={() => (selectable ? onToggleSelect?.(person) : onOpen(person))}
    >
      {/* 批量选择模式的勾选框 */}
      {selectable && (
        <div className="absolute left-2 top-2 z-10">
          <Checkbox
            checked={selected}
            aria-label={`选择 ${name || '未命名'}`}
            onClick={(e) => e.stopPropagation()}
            onCheckedChange={() => onToggleSelect?.(person)}
          />
        </div>
      )}
      <CardContent className="flex gap-4 p-4">
        {/* 头像：优先半身像，缺失时回退小头像 */}
        <div className="relative flex size-20 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-border bg-secondary/50">
          <span className="absolute font-display text-2xl text-muted-foreground/40">
            {(name || '?').slice(0, 1)}
          </span>
          <PersonAvatar
            id={person.headIconID}
            name={name}
            className="relative size-full object-cover object-top"
          />
        </div>

        <div className="flex min-w-0 flex-1 flex-col">
          <div className="flex items-center gap-2">
            <h3 className="truncate font-display text-lg leading-tight">{name || '未命名'}</h3>
            {person.nickName && (
              <span className="shrink-0 text-xs text-muted-foreground">字 {person.nickName}</span>
            )}
          </div>

          <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
            <Badge variant="outline" className="font-mono text-[10px]">
              #{person.Id}
            </Badge>
            <Badge variant="secondary" className="text-[10px]">
              {sexLabel}
            </Badge>
            <Badge variant="outline" className="text-[10px]">
              {labelOf(PERSON_TYPE_CHOICES, person.type)}
            </Badge>
            {person.state > 0 && (
              <Badge variant="secondary" className="text-[10px]">
                {stateLabel}
              </Badge>
            )}
            {!person.IsAlive && (
              <Badge variant="destructive" className="text-[10px]">
                已死亡
              </Badge>
            )}
            {/* 武将标签（仅自建武将库存在该字段） */}
            {(person.tags ?? []).map((tag) => (
              <Badge key={tag} variant="outline" className="border-gold/40 text-[10px] text-gold">
                #{tag}
              </Badge>
            ))}
          </div>

          {/* 五维能力（新版数据为 [基础值, 成长类型Id]，卡片只展示基础值） */}
          <div className="mt-3 grid grid-cols-5 gap-1">
            {ABILITY_FIELDS.map((f) => (
              <div key={f.key} className="flex flex-col items-center rounded bg-secondary/40 py-1">
                <span className="text-[10px] text-muted-foreground">{f.label}</span>
                <span className="text-sm font-semibold text-gold">{statBaseValue(person[f.key])}</span>
              </div>
            ))}
          </div>

          {/* 兵种适性 */}
          <div className="mt-2.5 flex flex-wrap gap-1">
            {WEAPON_FIELDS.map((f) => {
              const level = statBaseValue(person[f.key])
              return (
                <span
                  key={f.key}
                  className={cn(
                    'rounded px-1.5 py-0.5 text-[10px]',
                    level >= 3
                      ? 'bg-primary/15 text-primary'
                      : 'bg-secondary/50 text-muted-foreground',
                  )}
                >
                  {f.label} {abilityLevelLabel(level)}
                </span>
              )
            })}
          </div>

          {/* 最后修改时间（自建武将库专有） */}
          {person.updatedAt && (
            <p className="mt-2 text-[11px] text-muted-foreground">
              修改于 {formatUpdatedAt(person.updatedAt)}
            </p>
          )}
        </div>
      </CardContent>

      {/* 悬停操作（游客只读不展示；批量选择模式下隐藏，避免与勾选操作冲突） */}
      {!selectable && (
        <div className="absolute right-2 top-2 flex gap-1 opacity-0 transition-opacity duration-150 group-hover:opacity-100">
          {canEdit && (
            <Button
              variant="secondary"
              size="icon-sm"
              title="编辑"
              onClick={(e) => {
                e.stopPropagation()
                onOpen(person)
              }}
            >
              <Pencil />
            </Button>
          )}
          {canDelete && canEdit && (
            <Button
              variant="destructive"
              size="icon-sm"
              title="删除"
              onClick={(e) => {
                e.stopPropagation()
                onDelete(person)
              }}
            >
              <Trash2 />
            </Button>
          )}
        </div>
      )}
    </Card>
  )
}

/**
 * 武将头像。
 * 优先展示半身像（{id}_1.png），资源缺失时自动回退为头像（{id}_2.png），
 * 两者都不存在时不渲染图片，由外层展示姓名首字占位。
 * @param props 组件属性
 */
function PersonAvatar({
  id,
  name,
  className,
}: {
  /** 头像 ID（headIconID） */
  id: number
  /** 武将姓名（用于替代文本） */
  name: string
  /** 图片样式类名 */
  className?: string
}) {
  /** 加载阶段：0=半身像，1=头像，2=无资源 */
  const [stage, setStage] = useState(0)

  // 切换武将时重新从半身像开始尝试
  useEffect(() => {
    setStage(0)
  }, [id])

  if (stage >= 2) return null

  return (
    <img
      key={stage}
      src={headIconUrl(id, stage === 0 ? 1 : 2)}
      alt={name}
      loading="lazy"
      className={className}
      onError={() => setStage((s) => s + 1)}
    />
  )
}
