import { useState } from 'react'

/**
 * A club crest. The API serves real badge URLs, so those are used whenever they
 * are present; the lettered disc stays as the fallback for a missing or broken
 * image, with a colour derived from the name so a club always looks the same.
 */
const PALETTE = [
  'bg-[#C8102E]', // red
  'bg-[#004D98]', // blue
  'bg-[#6CABDD]', // sky
  'bg-[#DA291C]', // scarlet
  'bg-[#00A398]', // teal
  'bg-[#FDB913]', // amber
  'bg-[#7B2D8E]', // purple
  'bg-[#1B5E20]', // green
]

function colourFor(name: string) {
  let hash = 0
  for (let index = 0; index < name.length; index++) {
    hash = (hash * 31 + name.charCodeAt(index)) >>> 0
  }
  return PALETTE[hash % PALETTE.length]
}

const SIZES = {
  xs: 'size-5 text-[0.6rem]',
  sm: 'size-6 text-[0.65rem]',
  md: 'size-9 text-sm',
  lg: 'size-14 text-2xl',
} as const

export function TeamCrest({
  name,
  logoUrl,
  size = 'sm',
}: {
  name: string
  logoUrl?: string | null
  size?: keyof typeof SIZES
}) {
  const [failed, setFailed] = useState(false)

  if (logoUrl && !failed) {
    return (
      <img
        src={logoUrl}
        alt=""
        title={name}
        loading="lazy"
        onError={() => setFailed(true)}
        className={`shrink-0 object-contain ${SIZES[size]}`}
      />
    )
  }

  return (
    <span
      aria-hidden
      title={name}
      className={`flex shrink-0 items-center justify-center rounded-full font-black text-white ${
        SIZES[size]
      } ${colourFor(name)}`}
    >
      {name.charAt(0).toUpperCase()}
    </span>
  )
}
