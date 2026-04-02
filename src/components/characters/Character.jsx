import { useMemo } from 'react';

const teamColors = {
  A: '#3b82f6', // blue
  B: '#ef4444', // red
};

const Character = ({
  src,
  x,
  y,
  tileSize = 64,
  centerOffsetX = 0.5,
  centerOffsetY = 0.5,
  direction = 'right',
  cycleSeconds = 1.2,
  frames,
  frameW,
  frameH,
  moveMs = 0,
  opacity = 1,
  fadeMs = 0,
  team,
  hp,
  maxHp,
  typeLabel,
  highlight, // 'attacking' | 'damaged' | null
}) => {
  const safeFrames = useMemo(() => Math.max(1, Math.floor(frames || 1)), [frames]);
  const safeFrameW = useMemo(() => Math.max(1, Math.floor(frameW || tileSize)), [frameW, tileSize]);
  const safeFrameH = useMemo(() => Math.max(1, Math.floor(frameH || tileSize)), [frameH, tileSize]);
  const safeCycleSeconds = useMemo(() => Math.max(0.1, Number(cycleSeconds) || 1.2), [cycleSeconds]);

  const safeCenterOffsetX = useMemo(() => Number(centerOffsetX) || 0.5, [centerOffsetX]);
  const safeCenterOffsetY = useMemo(() => Number(centerOffsetY) || 0.5, [centerOffsetY]);

  const leftPos = Math.round((x + safeCenterOffsetX) * tileSize - safeFrameW / 2);
  const topPos = Math.round((y + safeCenterOffsetY) * tileSize - safeFrameH / 2);
  const zIndexVal = y;

  const safeMoveMs = useMemo(() => Math.max(0, Math.trunc(moveMs || 0)), [moveMs]);
  const safeFadeMs = useMemo(() => Math.max(0, Math.trunc(fadeMs || 0)), [fadeMs]);
  const safeOpacity = useMemo(() => {
    const v = Number(opacity);
    if (!Number.isFinite(v)) return 1;
    return Math.max(0, Math.min(1, v));
  }, [opacity]);

  const transitionProps = useMemo(() => {
    const props = [];
    if (safeMoveMs > 0) props.push('left', 'top');
    if (safeFadeMs > 0) props.push('opacity');
    return props;
  }, [safeFadeMs, safeMoveMs]);

  const transitionDurations = useMemo(() => {
    const durations = [];
    if (safeMoveMs > 0) durations.push(`${safeMoveMs}ms`, `${safeMoveMs}ms`);
    if (safeFadeMs > 0) durations.push(`${safeFadeMs}ms`);
    return durations;
  }, [safeFadeMs, safeMoveMs]);

  const hpFraction = maxHp > 0 ? Math.max(0, Math.min(1, hp / maxHp)) : 1;
  const barColor = team ? (teamColors[team] || '#888') : '#888';

  const glowColor = highlight === 'attacking' ? 'rgba(255, 200, 50, 0.7)'
    : highlight === 'damaged' ? 'rgba(255, 60, 60, 0.7)'
    : 'none';

  const containerStyle = {
    left: `${leftPos}px`,
    top: `${topPos}px`,
    zIndex: zIndexVal,
    width: `${safeFrameW}px`,
    height: `${safeFrameH}px`,
    opacity: safeOpacity,
    overflow: 'visible',
    position: 'absolute',
    imageRendering: 'pixelated',
    transformOrigin: 'center bottom',
    filter: glowColor !== 'none' ? `drop-shadow(0 0 6px ${glowColor}) drop-shadow(0 0 12px ${glowColor})` : 'none',

    transitionProperty: transitionProps.length ? transitionProps.join(', ') : 'none',
    transitionDuration: transitionDurations.length ? transitionDurations.join(', ') : '0ms',
    transitionTimingFunction: 'linear',
  };

  const spriteStyle = {
    width: `${safeFrameW}px`,
    height: `${safeFrameH}px`,
    overflow: 'hidden',
    imageRendering: 'pixelated',
    transform: direction === 'left' ? 'scaleX(-1)' : 'none',

    backgroundImage: `url(${src})`,
    backgroundRepeat: 'no-repeat',
    backgroundPosition: '0px 0px',
    backgroundSize: `${safeFrameW * safeFrames}px ${safeFrameH}px`,

    animationName: safeFrames > 1 ? 'sprite-bg' : 'none',
    animationDuration: `${safeCycleSeconds}s`,
    animationTimingFunction: `steps(${safeFrames})`,
    animationIterationCount: 'infinite',

    ['--sheetShift']: `${safeFrameW * safeFrames}px`,
  };

  const barWidth = Math.max(safeFrameW, 28);

  return (
    <div style={containerStyle}>
      {/* Type label */}
      {typeLabel && (
        <div style={{
          position: 'absolute',
          top: '-30px',
          left: '50%',
          transform: 'translateX(-50%)',
          fontSize: '7px',
          fontFamily: 'monospace',
          color: '#fff',
          backgroundColor: 'rgba(0,0,0,0.55)',
          padding: '1px 3px',
          borderRadius: '2px',
          whiteSpace: 'nowrap',
          pointerEvents: 'none',
          lineHeight: '1.2',
          letterSpacing: '0.3px',
        }}>
          {typeLabel}
        </div>
      )}

      {/* HP text */}
      {hp != null && maxHp > 0 && (
        <div style={{
          position: 'absolute',
          top: '-20px',
          left: '50%',
          transform: 'translateX(-50%)',
          fontSize: '7px',
          fontFamily: 'monospace',
          fontWeight: 'bold',
          color: hpFraction > 0.5 ? '#6fe86f' : hpFraction > 0.25 ? '#f0c040' : '#ff5555',
          textShadow: '0 0 3px rgba(0,0,0,0.9), 0 1px 1px rgba(0,0,0,0.8)',
          whiteSpace: 'nowrap',
          pointerEvents: 'none',
          lineHeight: '1',
          letterSpacing: '0.3px',
          textAlign: 'center',
        }}>
          {Math.round(hp)}/{maxHp}
        </div>
      )}

      {/* HP bar */}
      {hp != null && maxHp > 0 && (
        <div style={{
          position: 'absolute',
          top: '-11px',
          left: '50%',
          transform: 'translateX(-50%)',
          width: `${barWidth}px`,
          height: '4px',
          backgroundColor: 'rgba(0,0,0,0.45)',
          borderRadius: '2px',
          overflow: 'hidden',
          pointerEvents: 'none',
        }}>
          <div style={{
            width: `${hpFraction * 100}%`,
            height: '100%',
            backgroundColor: barColor,
            borderRadius: '2px',
            transition: 'width 200ms linear',
          }} />
        </div>
      )}

      {/* Team dot */}
      {team && (
        <div style={{
          position: 'absolute',
          top: '-10px',
          left: '50%',
          transform: `translateX(${barWidth / 2 + 3}px)`,
          width: '5px',
          height: '5px',
          borderRadius: '50%',
          backgroundColor: barColor,
          border: '1px solid rgba(255,255,255,0.6)',
          pointerEvents: 'none',
        }} />
      )}

      {/* Sprite */}
      <div className="character-sprite" style={spriteStyle} />
    </div>
  );
};

export default Character;
