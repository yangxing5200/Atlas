<script setup lang="ts">
import { computed } from 'vue'

type PreviewBlock =
  | { type: 'heading'; level: number; text: string }
  | { type: 'paragraph'; text: string }
  | { type: 'table'; headers: string[]; rows: string[][] }

const props = withDefaults(defineProps<{
  text?: string | null
  variant?: 'document' | 'extracted'
}>(), {
  variant: 'document',
})

const blocks = computed(() => parsePreviewText(props.text || ''))

function parsePreviewText(value: string): PreviewBlock[] {
  const lines = value.replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n')
  const result: PreviewBlock[] = []
  let index = 0
  let lastHeading = ''

  while (index < lines.length) {
    const line = lines[index]
    if (!line.trim()) {
      index += 1
      continue
    }

    const headingMatch = /^(#{1,6})\s+(.+)$/.exec(line.trim())
    if (headingMatch) {
      const text = headingMatch[2].trim()
      lastHeading = text
      result.push({ type: 'heading', level: headingMatch[1].length, text })
      index += 1
      continue
    }

    if (isMarkdownTableStart(lines, index)) {
      const parsed = parseMarkdownTable(lines, index, lastHeading)
      if (parsed.block.rows.length > 0) {
        result.push(parsed.block)
        index = parsed.nextIndex
        continue
      }
    }

    const paragraphLines: string[] = []
    while (index < lines.length) {
      const current = lines[index]
      if (!current.trim() || /^(#{1,6})\s+(.+)$/.test(current.trim()) || isMarkdownTableStart(lines, index)) {
        break
      }
      paragraphLines.push(current)
      index += 1
    }

    if (paragraphLines.length > 0) {
      result.push({ type: 'paragraph', text: paragraphLines.join('\n').trimEnd() })
    } else {
      index += 1
    }
  }

  return result
}

function isMarkdownTableStart(lines: string[], index: number) {
  return index + 1 < lines.length && isMarkdownRow(lines[index]) && isMarkdownSeparator(lines[index + 1])
}

function parseMarkdownTable(lines: string[], startIndex: number, nearbyHeading: string) {
  let headers = splitMarkdownRow(lines[startIndex])
  let cursor = startIndex + 2
  const rows: string[][] = []

  while (cursor < lines.length && isMarkdownRow(lines[cursor])) {
    const cells = splitMarkdownRow(lines[cursor])
    if (cells.some((cell) => cell.trim())) {
      rows.push(cells)
    }
    cursor += 1
  }

  const promoted = promoteContinuationHeader(headers, rows)
  headers = fillBlankQualificationHeaders(nearbyHeading, promoted.headers)
  const columnCount = Math.max(headers.length, ...promoted.rows.map((row) => row.length))

  return {
    block: {
      type: 'table' as const,
      headers: normalizeRow(headers, columnCount),
      rows: promoted.rows.map((row) => normalizeRow(row, columnCount)),
    },
    nextIndex: cursor,
  }
}

function promoteContinuationHeader(headers: string[], rows: string[][]) {
  if (rows.length === 0) return { headers, rows }

  const merged = mergeHeaderRows(headers, rows[0])
  if (scoreHeaderRow(merged) <= scoreHeaderRow(headers)) {
    return { headers, rows }
  }

  return {
    headers: merged,
    rows: rows.slice(1),
  }
}

function mergeHeaderRows(parent: string[], child: string[]) {
  const columnCount = Math.max(parent.length, child.length)
  const merged: string[] = []
  for (let index = 0; index < columnCount; index += 1) {
    merged.push((child[index] || parent[index] || '').trim())
  }
  return merged
}

function fillBlankQualificationHeaders(nearbyHeading: string, headers: string[]) {
  if (
    headers.length < 6 ||
    !containsAny(nearbyHeading, ['专用资格要求', '资格要求']) ||
    !headers.some((header) => normalizeKey(header).includes(normalizeKey('资质要求'))) ||
    !headers.some((header) => normalizeKey(header).includes(normalizeKey('业绩要求'))) ||
    !headers.some((header) => normalizeKey(header).includes(normalizeKey('人员要求')))
  ) {
    return headers
  }

  const filled = [...headers]
  const defaultHeaders = ['分标', '包号', '包名称']
  defaultHeaders.forEach((label, index) => {
    if (!filled[index]?.trim()) filled[index] = label
  })
  return filled
}

function scoreHeaderRow(headers: string[]) {
  const tokens = [
    '分标编号',
    '分标名称',
    '分标',
    '标段',
    '包号',
    '包名称',
    '采购范围',
    '项目内容',
    '服务期',
    '服务期限',
    '框架协议有效期',
    '实施地点',
    '交货地点',
    '服务地点',
    '资质要求',
    '资格要求',
    '业绩要求',
    '人员要求',
  ].map(normalizeKey)

  return tokens.filter((token) => headers.some((header) => normalizeKey(header).includes(token))).length
}

function normalizeRow(row: string[], columnCount: number) {
  const normalized = row.slice(0, columnCount).map((cell) => cell.trim())
  while (normalized.length < columnCount) normalized.push('')
  return normalized
}

function isMarkdownRow(line: string) {
  const trimmed = line.trim()
  return trimmed.length >= 2 && trimmed.startsWith('|') && trimmed.endsWith('|')
}

function isMarkdownSeparator(line: string) {
  if (!isMarkdownRow(line)) return false
  const cells = splitMarkdownRow(line)
  return cells.length > 0 && cells.every((cell) => /^:?-{3,}:?$/.test(cell.replace(/\s/g, '')))
}

function splitMarkdownRow(line: string) {
  return line
    .trim()
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split('|')
    .map((cell) => cell.trim())
}

function normalizeKey(value: string) {
  return value.replace(/[\s|:：\-_()（）/／]/g, '').toLowerCase()
}

function containsAny(value: string, tokens: string[]) {
  return tokens.some((token) => value.includes(token))
}

function headingTag(level: number) {
  return level <= 2 ? 'h3' : 'h4'
}
</script>

<template>
  <article class="raw-notice-preview" :class="`raw-notice-preview--${variant}`">
    <template v-if="blocks.length > 0">
      <template v-for="(block, blockIndex) in blocks" :key="blockIndex">
        <component :is="headingTag(block.level)" v-if="block.type === 'heading'" class="preview-heading">
          {{ block.text }}
        </component>

        <p v-else-if="block.type === 'paragraph'" class="preview-paragraph">{{ block.text }}</p>

        <div v-else class="preview-table-wrap">
          <table class="preview-table">
            <thead>
              <tr>
                <th v-for="(header, headerIndex) in block.headers" :key="headerIndex">
                  {{ header || `列${headerIndex + 1}` }}
                </th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, rowIndex) in block.rows" :key="rowIndex">
                <td v-for="(cell, cellIndex) in row" :key="cellIndex">{{ cell || '-' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </template>
    <span v-else>-</span>
  </article>
</template>

<style scoped>
.raw-notice-preview {
  min-height: 220px;
  max-height: 560px;
  overflow: auto;
  padding: 14px;
  margin: 0;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
  color: #273444;
  font-size: 13px;
  line-height: 1.75;
  word-break: break-word;
}

.raw-notice-preview--document {
  font-family: inherit;
}

.raw-notice-preview--extracted {
  background: #f8fafc;
  font-family: inherit;
}

.preview-heading {
  margin: 14px 0 10px;
  color: #172033;
  font-size: 15px;
  line-height: 1.4;
  font-weight: 700;
}

.preview-heading:first-child {
  margin-top: 0;
}

.preview-paragraph {
  margin: 0 0 10px;
  white-space: pre-wrap;
}

.preview-table-wrap {
  max-width: 100%;
  margin: 12px 0 18px;
  overflow-x: auto;
  border: 1px solid #d8e0ec;
  border-radius: 6px;
  background: #fff;
}

.preview-table {
  width: max-content;
  min-width: 100%;
  border-collapse: collapse;
  color: #253247;
  font-size: 12px;
  line-height: 1.55;
}

.preview-table th,
.preview-table td {
  max-width: 420px;
  padding: 8px 10px;
  border-right: 1px solid #e1e7f0;
  border-bottom: 1px solid #e1e7f0;
  text-align: left;
  vertical-align: top;
  white-space: normal;
}

.preview-table th {
  position: sticky;
  top: 0;
  z-index: 1;
  background: #f1f5f9;
  color: #172033;
  font-weight: 700;
}

.preview-table tr:last-child td {
  border-bottom: 0;
}

.preview-table th:last-child,
.preview-table td:last-child {
  border-right: 0;
}
</style>
