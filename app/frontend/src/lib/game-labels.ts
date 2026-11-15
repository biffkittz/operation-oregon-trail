import type { Difficulty, GameStatus, GameTone, NodeKind, StatKind, TextSpeed, Verdict } from '@/api/types'

export const toneOptions: ReadonlyArray<{ value: GameTone; label: string; description: string }> = [
  { value: 'Whimsical', label: 'Whimsical', description: 'Absurd humor and warm hearts.' },
  { value: 'Hopeful', label: 'Hopeful', description: 'Earnest optimism and wonder.' },
  { value: 'Gritty', label: 'Gritty', description: 'Dust, debt and dark jokes.' },
  { value: 'Morose', label: 'Morose', description: 'Bleak, poetic, small mercies.' },
]

export const difficultyOptions: ReadonlyArray<{ value: Difficulty; label: string; description: string }> = [
  { value: 'Easy', label: 'Easy', description: 'Generous supplies, long time limits.' },
  { value: 'Normal', label: 'Normal', description: 'The trail as intended.' },
  { value: 'Hard', label: 'Hard', description: 'Lean supplies, short fuses.' },
]

export const textSpeedOptions: ReadonlyArray<{ value: TextSpeed; label: string; msPerChar: number }> = [
  { value: 'Instant', label: 'Instant', msPerChar: 0 },
  { value: 'Fast', label: 'Fast', msPerChar: 6 },
  { value: 'Normal', label: 'Normal', msPerChar: 16 },
  { value: 'Slow', label: 'Slow', msPerChar: 32 },
]

/** One-glyph icons (plain text, so screen readers get the label instead). */
export const nodeKindInfo: Record<NodeKind, { label: string; glyph: string }> = {
  Origin: { label: 'Station', glyph: '◎' },
  Outpost: { label: 'Outpost', glyph: '⌂' },
  Settlement: { label: 'Settlement', glyph: '⌘' },
  Derelict: { label: 'Derelict', glyph: '☒' },
  Hazard: { label: 'Hazard', glyph: '⚠' },
  Raiders: { label: 'Raiders', glyph: '☠' },
  Anomaly: { label: 'Anomaly', glyph: '✶' },
  Landmark: { label: 'Landmark', glyph: '▲' },
  Destination: { label: 'Willamette Landing', glyph: '★' },
}

export const statInfo: Record<StatKind, { label: string; blurb: string }> = {
  Creativity: { label: 'Creativity', blurb: 'Imagination and showmanship' },
  Morality: { label: 'Morality', blurb: 'Judgment and compassion' },
  Resilience: { label: 'Resilience', blurb: 'Grit under pressure' },
  Diplomacy: { label: 'Diplomacy', blurb: 'Persuasion and bargaining' },
  Ingenuity: { label: 'Ingenuity', blurb: 'Engineering and problem-solving' },
  Reflexes: { label: 'Reflexes', blurb: 'Gunnery in skirmishes' },
}

export const verdictInfo: Record<Verdict, { label: string; tone: 'good' | 'mixed' | 'bad' }> = {
  CriticalSuccess: { label: 'Triumph', tone: 'good' },
  Success: { label: 'Success', tone: 'good' },
  Partial: { label: 'Mixed', tone: 'mixed' },
  Failure: { label: 'Setback', tone: 'bad' },
  CriticalFailure: { label: 'Disaster', tone: 'bad' },
}

export const statusInfo: Record<GameStatus, { label: string; tone: 'good' | 'mixed' | 'bad' }> = {
  Active: { label: 'On the trail', tone: 'mixed' },
  Victory: { label: 'Arrived', tone: 'good' },
  Defeat: { label: 'Lost', tone: 'bad' },
}
