import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { calculateRandomTeam } from "../api/battleCalculator";
import { units as unitDefs } from "../units";
import Character from "./characters/Character";

const stableHash = (str) => {
  let h = 2166136261;
  for (let i = 0; i < str.length; i += 1) {
    h ^= str.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
};

const normalizeActionTypeKey = (value) =>
  String(value ?? "")
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "");

const normalizeUnitName = (action) => {
  const name = action?.unitName;
  if (typeof name !== "string") return null;
  const trimmed = name.trim();
  return trimmed ? trimmed : null;
};

const normalizeHealthLossKey = (key) => {
  const k = normalizeActionTypeKey(key);
  if (k === "looseshealth" || k === "loseshealth" || k === "losehealth" || k === "loseslife") return "looseshealth";
  return k;
};

const normalizeSpriteKey = (value) =>
  String(value ?? "")
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "");

const Arena = () => {
  const blurredBackgroundSrc = "/terrain.png";
  const centeredImageSrc = "/terrain.png";
  const titleScreenSrc = "/title_screen.png";
  const tileSize = 32;
  const unitsPerTeam = 12;

  const welcomeFadeMs = 280;
  const playerTeamFadeMs = 220;

  const [terrainLoaded, setTerrainLoaded] = useState(false);
  const [terrainSize, setTerrainSize] = useState({ w: 0, h: 0 });

  const [teamResponse, setTeamResponse] = useState(null);
  const [, setTeamLoading] = useState(false);
  const [teamError, setTeamError] = useState(null);

  const [units, setUnits] = useState([]);
  const [battleWinner, setBattleWinner] = useState(null);

  const [gameStarted, setGameStarted] = useState(false);
  const [showWelcome, setShowWelcome] = useState(true);
  const [welcomeFading, setWelcomeFading] = useState(false);

  const [playerTeam, setPlayerTeam] = useState(null);
  const [playerTeamLabelVisible, setPlayerTeamLabelVisible] = useState(false);
  const [playerTeamLabelOpaque, setPlayerTeamLabelOpaque] = useState(false);

  const [battleLog, setBattleLog] = useState([]);
  const logRef = useRef(null);

  // Playback control state
  const [currentStep, setCurrentStep] = useState(-1);
  const [isPlaying, setIsPlaying] = useState(false);
  const [playbackSpeed, setPlaybackSpeed] = useState(1);
  const isPlayingRef = useRef(false);
  isPlayingRef.current = isPlaying;

  // Auto-scroll log to bottom
  useEffect(() => {
    if (logRef.current) {
      logRef.current.scrollTop = logRef.current.scrollHeight;
    }
  }, [battleLog]);

  useEffect(() => {
    if (!gameStarted) return;
    let cancelled = false;

    const run = async () => {
      setTeamLoading(true);
      setTeamError(null);

      try {
        const body = {
          battleId: crypto.randomUUID?.() ?? `${Date.now()}`,
          winner: "",
          actions: [],
        };

        const data = await calculateRandomTeam(body);
        if (!cancelled) {
          setTeamResponse(data);
          console.log("Random team response:", data);
        }
      } catch (err) {
        if (!cancelled) {
          setTeamError(err);
          console.error("Random team API error:", err);
        }
      } finally {
        if (!cancelled) setTeamLoading(false);
      }
    };

    run();
    return () => {
      cancelled = true;
    };
  }, [gameStarted]);

  const inferTeamFromUnitName = useCallback((name) => {
    if (typeof name !== "string") return null;
    const lower = name.trim().toLowerCase();
    if (!lower) return null;
    if (lower.startsWith("teama") || lower.startsWith("team_a")) return "A";
    if (lower.startsWith("teamb") || lower.startsWith("team_b")) return "B";
    return null;
  }, []);

  // After pressing Play, infer the player's team from the backend actions.
  useEffect(() => {
    if (!gameStarted) return;
    if (!teamResponse) return;

    const rawActions =
      teamResponse.actions || teamResponse.battle?.actions || teamResponse.result?.actions || [];
    if (!Array.isArray(rawActions) || rawActions.length === 0) return;

    const findFirstByActionType = (wantedKey) => {
      for (const a of rawActions) {
        if (!a || typeof a !== "object") continue;
        const k = normalizeActionTypeKey(a.actionType);
        if (k === wantedKey) return a;
      }
      return null;
    };

    const firstMove = findFirstByActionType("moves");
    const firstNonAppears =
      firstMove ??
      rawActions.find((a) => a && typeof a === "object" && normalizeActionTypeKey(a.actionType) !== "appears") ??
      null;

    const unitName = normalizeUnitName(firstNonAppears);
    const team = inferTeamFromUnitName(unitName);
    if (!team) return;

    setPlayerTeam(team);
    setPlayerTeamLabelVisible(true);
    setPlayerTeamLabelOpaque(false);

    const fadeInId = window.setTimeout(() => setPlayerTeamLabelOpaque(true), 0);

    return () => {
      window.clearTimeout(fadeInId);
    };
  }, [gameStarted, inferTeamFromUnitName, teamResponse]);

  const spriteSheets = useMemo(
    () => [
      { src: "/boar.png", frames: 8, footprintW: 2, footprintH: 1 },
      { src: "/chaos.png", frames: 10, footprintW: 1, footprintH: 1 },
      { src: "/dracula.png", frames: 6, footprintW: 1, footprintH: 1 },
      { src: "/slime.png", frames: 6, footprintW: 1, footprintH: 1 },
      { src: "/fire_mage.png", frames: 8, footprintW: 1, footprintH: 1 },
      { src: "/goblin.png", frames: 6, footprintW: 1, footprintH: 1 },
      { src: "/reaper.png", frames: 7, footprintW: 1, footprintH: 2 },
      { src: "/robed_spirit.png", frames: 8, footprintW: 1, footprintH: 2 },
      { src: "/skeleton.png", frames: 7, footprintW: 1, footprintH: 1 },
    ],
    []
  );

  const spriteIndexByName = useMemo(() => {
    return {
      boar: 0,
      chaos: 1,
      dracula: 2,
      slime: 3,
      firemage: 4,
      goblin: 5,
      reaper: 6,
      robedspirit: 7,
      skeleton: 8,
    };
  }, []);

  const spriteIndicesByBackendUnitType = useMemo(
    () => ({
      heavy: [spriteIndexByName.boar, spriteIndexByName.reaper],
      light: [spriteIndexByName.skeleton, spriteIndexByName.chaos],
      fast: [spriteIndexByName.dracula],
      shortrange: [spriteIndexByName.slime, spriteIndexByName.goblin],
      longrange: [spriteIndexByName.firemage, spriteIndexByName.robedspirit],
    }),
    [spriteIndexByName]
  );

  const typeLabelBySpriteIndex = useMemo(() => {
    const map = {};
    const labels = {
      heavy: "Heavy",
      light: "Light",
      fast: "Fast",
      shortrange: "Short Range",
      longrange: "Long Range",
    };
    for (const [key, indices] of Object.entries(spriteIndicesByBackendUnitType)) {
      for (const idx of indices) {
        map[idx] = labels[key] || key;
      }
    }
    return map;
  }, [spriteIndicesByBackendUnitType]);

  const legendEntries = useMemo(() => {
    const entries = [];
    const typeOrder = ["heavy", "light", "fast", "shortrange", "longrange"];
    const labels = {
      heavy: "Heavy",
      light: "Light",
      fast: "Fast",
      shortrange: "Short Range",
      longrange: "Long Range",
    };
    for (const type of typeOrder) {
      const indices = spriteIndicesByBackendUnitType[type] || [];
      for (const idx of indices) {
        const sheet = spriteSheets[idx];
        if (!sheet) continue;
        entries.push({ src: sheet.src, frames: sheet.frames, label: labels[type] });
      }
    }
    return entries;
  }, [spriteIndicesByBackendUnitType, spriteSheets]);

  const { cols, rows } = useMemo(() => {
    const colsVal = terrainSize.w ? Math.floor(terrainSize.w / tileSize) : 0;
    const rowsVal = terrainSize.h ? Math.floor(terrainSize.h / tileSize) : 0;
    return { cols: Math.max(0, colsVal), rows: Math.max(0, rowsVal) };
  }, [terrainSize.h, terrainSize.w, tileSize]);

  const placements = useMemo(() => {
    if (!terrainLoaded || cols === 0 || rows === 0) return [];

    const playableMinX = 1;
    const playableMinY = 0;
    const playableMaxX = cols - 1;
    const playableMaxY = rows - 1;

    const playableCols = playableMaxX - playableMinX;
    const playableRows = playableMaxY - playableMinY;

    const cellToCoord = (x, y) => {
      const colIndex = x - playableMinX;
      const rowIndexFromBottom = (playableMaxY - 1) - y;
      if (colIndex < 0 || colIndex >= playableCols) return null;
      if (rowIndexFromBottom < 0 || rowIndexFromBottom >= playableRows) return null;

      const letter = String.fromCharCode(65 + colIndex);
      const number = rowIndexFromBottom + 1;
      return `${letter}${number}`;
    };

    const riverCol = playableMinX + Math.floor(playableCols / 2);
    const riverRow = playableMinY + Math.floor(playableRows / 2);

    const isOutOfBounds = (x, y) =>
      x < playableMinX || x >= playableMaxX || y < playableMinY || y >= playableMaxY;
    const isRiver = (x, y) => x === riverCol || y === riverRow;

    const inTeamAZone = (x) => x < riverCol;
    const inTeamBZone = (x) => x > riverCol;

    const desiredPerTeam = Math.max(1, unitsPerTeam);

    let idCounter = 0;

    const occupied = new Set();
    const keyOf = (x, y) => `${x},${y}`;

    const spriteAt = (spriteIndex) => spriteSheets[spriteIndex] ?? spriteSheets[0];
    const randomInt = (minInclusive, maxInclusive) =>
      Math.floor(Math.random() * (maxInclusive - minInclusive + 1)) + minInclusive;
    const randomSpriteIndex = () => Math.floor(Math.random() * spriteSheets.length);

    const shuffleInPlace = (arr) => {
      for (let i = arr.length - 1; i > 0; i -= 1) {
        const j = Math.floor(Math.random() * (i + 1));
        [arr[i], arr[j]] = [arr[j], arr[i]];
      }
      return arr;
    };

    const buildSpritePlan = () => {
      const plan = [];
      for (let i = 0; i < desiredPerTeam; i += 1) {
        plan.push(i < spriteSheets.length ? i : randomSpriteIndex());
      }
      return shuffleInPlace(plan);
    };

    const getFootprintCells = (x, y, sprite) => {
      const wTiles = Math.max(1, Math.floor(sprite.footprintW || 1));
      const hTiles = Math.max(1, Math.floor(sprite.footprintH || 1));
      const cells = [];
      for (let dy = 0; dy < hTiles; dy += 1) {
        for (let dx = 0; dx < wTiles; dx += 1) {
          cells.push({ x: x + dx, y: y + dy });
        }
      }
      return cells;
    };

    const canPlace = (team, x, y, sprite) => {
      const wTiles = Math.max(1, Math.floor(sprite.footprintW || 1));
      const hTiles = Math.max(1, Math.floor(sprite.footprintH || 1));

      if (team === "A" && !inTeamAZone(x)) return false;
      if (team === "B" && !inTeamBZone(x)) return false;

      for (let dy = 0; dy < hTiles; dy += 1) {
        for (let dx = 0; dx < wTiles; dx += 1) {
          const cx = x + dx;
          const cy = y + dy;

          if (isOutOfBounds(cx, cy)) return false;
          if (isRiver(cx, cy)) return false;
          if (team === "A" && !inTeamAZone(cx)) return false;
          if (team === "B" && !inTeamBZone(cx)) return false;
          if (occupied.has(keyOf(cx, cy))) return false;
        }
      }

      return true;
    };

    const commitPlacement = (team, x, y, spriteIndex) => {
      const sprite = spriteAt(spriteIndex);
      const wTiles = Math.max(1, Math.floor(sprite.footprintW || 1));
      const hTiles = Math.max(1, Math.floor(sprite.footprintH || 1));

      for (const cell of getFootprintCells(x, y, sprite)) {
        occupied.add(keyOf(cell.x, cell.y));
      }

      return {
        id: `u${idCounter++}`,
        team,
        x,
        y,
        spriteIndex,
        footprintW: wTiles,
        footprintH: hTiles,
        coord: cellToCoord(x, y),
        direction: team === "A" ? "right" : "left",
      };
    };

    const tryPlaceOne = (team, spriteIndex, maxAttempts = 1500) => {
      for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
        const sprite = spriteAt(spriteIndex);
        const wTiles = Math.max(1, Math.floor(sprite.footprintW || 1));
        const hTiles = Math.max(1, Math.floor(sprite.footprintH || 1));

        const minX = playableMinX;
        const maxX = playableMaxX - wTiles;
        const minY = playableMinY;
        const maxY = playableMaxY - hTiles;

        if (maxX < minX || maxY < minY) return null;

        let x = randomInt(minX, maxX);
        if (team === "A") x = randomInt(minX, Math.min(maxX, riverCol - 1));
        if (team === "B") x = randomInt(Math.max(minX, riverCol + 1), maxX);

        const y = randomInt(minY, maxY);

        if (!canPlace(team, x, y, sprite)) continue;
        return commitPlacement(team, x, y, spriteIndex);
      }

      return null;
    };

    const results = [];

    const planA = buildSpritePlan();
    const planB = buildSpritePlan();

    for (let i = 0; i < desiredPerTeam; i += 1) {
      const placed = tryPlaceOne("A", planA[i]);
      if (!placed) break;
      results.push(placed);
    }
    for (let i = 0; i < desiredPerTeam; i += 1) {
      const placed = tryPlaceOne("B", planB[i]);
      if (!placed) break;
      results.push(placed);
    }

    return results;
  }, [cols, rows, spriteSheets, terrainLoaded, unitsPerTeam]);

  // Seed unit state from placements once terrain is ready.
  useEffect(() => {
    if (!terrainLoaded) return;
    if (!gameStarted) return;
    if (!placements.length) return;
    setUnits((prev) => {
      if (prev.length) return prev;
      return placements.map((p) => {
        const def = unitDefs.find((u) => u.unitType === p.spriteIndex);
        return {
          ...p,
          unitType: p.spriteIndex,
          unitName: def?.unitName ?? `Unit ${p.spriteIndex}`,
          actionType: null,
          target: "",
          amount: 0,
          destination: "",
          hp: 100,
          maxHp: 100,
          moveMs: 0,
          opacity: 1,
          fadeMs: 0,
          isDying: false,
        };
      });
    });
  }, [gameStarted, placements, terrainLoaded]);

  const pickSpriteIndexFromBackend = useCallback((action) => {
    const t = action?.unitType;
    if (Number.isFinite(t)) {
      const idx = Math.trunc(t);
      return idx >= 0 && idx < spriteSheets.length ? idx : 0;
    }

    const s = typeof t === "string" ? t.trim() : "";
    const unitName = normalizeUnitName(action) ?? "";

    const typeKey = normalizeSpriteKey(s);
    const mappedSet = spriteIndicesByBackendUnitType[typeKey];
    if (Array.isArray(mappedSet) && mappedSet.length > 0) {
      const pick = mappedSet[stableHash(unitName || s) % mappedSet.length];
      return Number.isFinite(pick) ? pick : 0;
    }

    const keyFromType = normalizeSpriteKey(s);
    const mappedFromType = spriteIndexByName[keyFromType];
    if (Number.isFinite(mappedFromType)) return mappedFromType;

    const keyFromName = normalizeSpriteKey(unitName);
    const mappedFromName = spriteIndexByName[keyFromName];
    if (Number.isFinite(mappedFromName)) return mappedFromName;

    return stableHash(s || unitName || "unit") % spriteSheets.length;
  }, [spriteIndexByName, spriteIndicesByBackendUnitType, spriteSheets.length]);

  // ---------------------------------------------------------------------------
  // Snapshot-based playback: pre-compute all action states
  // ---------------------------------------------------------------------------
  const { snapshots: computedSnapshots, winner: computedWinner } = useMemo(() => {
    if (!gameStarted || !teamResponse || !terrainLoaded || cols === 0 || rows === 0 || teamError) {
      return { snapshots: [], winner: null };
    }

    const rawActions =
      teamResponse.actions || teamResponse.battle?.actions || teamResponse.result?.actions || [];
    if (!Array.isArray(rawActions) || rawActions.length === 0) {
      return { snapshots: [], winner: null };
    }

    const playableMinX = 1;
    const playableMinY = 0;
    const playableMaxX = cols - 1;
    const playableMaxY = rows - 1;

    const playableCols = playableMaxX - playableMinX;
    const playableRows = playableMaxY - playableMinY;

    const riverCol = playableMinX + Math.floor(playableCols / 2);
    const riverRow = playableMinY + Math.floor(playableRows / 2);
    const isRiver = (x, y) => x === riverCol || y === riverRow;

    const cellToCoord = (x, y) => {
      const colIndex = x - playableMinX;
      const rowIndexFromBottom = (playableMaxY - 1) - y;
      if (colIndex < 0 || colIndex >= playableCols) return null;
      if (rowIndexFromBottom < 0 || rowIndexFromBottom >= playableRows) return null;
      const letter = String.fromCharCode(65 + colIndex);
      const number = rowIndexFromBottom + 1;
      return `${letter}${number}`;
    };

    const parseDestination = (destination) => {
      if (!destination) return null;

      if (typeof destination === "object") {
        const xVal = destination.x ?? destination.col ?? destination.column;
        const yVal = destination.y ?? destination.row;
        if (Number.isFinite(xVal) && Number.isFinite(yVal)) {
          const xNum = Math.trunc(xVal);
          const yNum = Math.trunc(yVal);
          const x = xNum >= 0 && xNum < playableCols ? playableMinX + xNum : xNum;
          const y = yNum >= 0 && yNum < playableRows ? playableMinY + yNum : yNum;
          return { x, y };
        }
        return null;
      }

      if (typeof destination !== "string") return null;
      const trimmed = destination.trim();
      if (!trimmed) return null;

      const mPair = trimmed.match(/^(-?\d+)\s*[, :]\s*(-?\d+)$/);
      if (mPair) {
        const xNum = Number(mPair[1]);
        const yNum = Number(mPair[2]);
        if (!Number.isFinite(xNum) || !Number.isFinite(yNum)) return null;
        const x0 = Math.trunc(xNum);
        const y0 = Math.trunc(yNum);
        const x = x0 >= 0 && x0 < playableCols ? playableMinX + x0 : x0;
        const y = y0 >= 0 && y0 < playableRows ? playableMinY + y0 : y0;
        return { x, y };
      }

      const mCoord = trimmed.match(/^([A-Za-z])\s*(\d+)$/);
      if (mCoord) {
        const letter = mCoord[1].toUpperCase();
        const number = Number(mCoord[2]);
        if (!Number.isFinite(number)) return null;
        const x = playableMinX + (letter.charCodeAt(0) - 65);
        const y = (playableMaxY - 1) - (number - 1);
        return { x, y };
      }

      return null;
    };

    const playableColsMid = playableMinX + Math.floor(playableCols / 2);

    const getFootprintCells = (x, y, unitLike) => {
      const wTiles = Math.max(1, Math.floor(unitLike.footprintW || 1));
      const hTiles = Math.max(1, Math.floor(unitLike.footprintH || 1));
      const cells = [];
      for (let dy = 0; dy < hTiles; dy += 1) {
        for (let dx = 0; dx < wTiles; dx += 1) {
          cells.push({ x: x + dx, y: y + dy });
        }
      }
      return cells;
    };

    const canOccupyAt = (unitsMap, uName, x, y, unitLike) => {
      const occupied = new Set();
      const keyOf = (xx, yy) => `${xx},${yy}`;
      for (const [otherName, other] of unitsMap.entries()) {
        if (otherName === uName) continue;
        for (const c of getFootprintCells(other.x, other.y, other)) {
          occupied.add(keyOf(c.x, c.y));
        }
      }
      for (const c of getFootprintCells(x, y, unitLike)) {
        if (c.x < playableMinX || c.x >= playableMaxX || c.y < playableMinY || c.y >= playableMaxY)
          return false;
        if (isRiver(c.x, c.y)) return false;
        if (occupied.has(keyOf(c.x, c.y))) return false;
      }
      return true;
    };

    const actionStepMs = (action, actionKey) => {
      const ms = action?.durationMs ?? action?.stepMs ?? action?.ms;
      if (Number.isFinite(ms)) return Math.max(0, Math.trunc(ms));
      if (actionKey === "moves") return 450;
      return 250;
    };

    const syncFields = (u, a) => ({
      ...u,
      unitType: a.unitType ?? u.unitType,
      actionType: a.actionType ?? u.actionType,
      target: typeof a.target === "string" ? a.target : u.target,
      amount: Number.isFinite(a.amount) ? a.amount : u.amount,
      destination: typeof a.destination === "string" ? a.destination : u.destination,
    });

    const cloneMap = (m) => {
      const arr = [];
      for (const v of m.values()) arr.push({ ...v });
      return arr;
    };

    // Build snapshots
    const result = [];
    const unitsByName = new Map();

    for (const a of rawActions) {
      if (!a || typeof a !== "object") continue;
      const unitName = normalizeUnitName(a);
      if (!unitName) continue;

      const actionKey = normalizeHealthLossKey(a.actionType);
      const stepMs = actionStepMs(a, actionKey);

      const existing = unitsByName.get(unitName);
      if (existing?.isDying && actionKey !== "dies") continue;

      const baseSpriteIndex = existing ? existing.spriteIndex : pickSpriteIndexFromBackend(a);
      const baseSprite = spriteSheets[baseSpriteIndex] ?? spriteSheets[0];
      const baseFootprintW = Math.max(1, Math.floor(baseSprite.footprintW || (existing?.footprintW ?? 1)));
      const baseFootprintH = Math.max(1, Math.floor(baseSprite.footprintH || (existing?.footprintH ?? 1)));

      if (actionKey === "appears") {
        const cell = parseDestination(a.destination ?? a.to ?? a.dest ?? a.position ?? a.cell ?? null);
        if (!cell) continue;
        if (cell.x < playableMinX || cell.x >= playableMaxX || cell.y < playableMinY || cell.y >= playableMaxY) continue;
        if (isRiver(cell.x, cell.y)) continue;

        const lower = unitName.toLowerCase();
        const teamFromName =
          lower.startsWith("teama") || lower.startsWith("team_a") ? "A"
          : lower.startsWith("teamb") || lower.startsWith("team_b") ? "B"
          : null;
        const team = teamFromName ?? (cell.x < playableColsMid ? "A" : "B");
        const direction = team === "A" ? "right" : "left";

        const candidate = syncFields({
          id: unitName,
          team,
          x: cell.x,
          y: cell.y,
          spriteIndex: baseSpriteIndex,
          footprintW: baseFootprintW,
          footprintH: baseFootprintH,
          coord: cellToCoord(cell.x, cell.y),
          direction,
          unitType: a.unitType,
          unitName,
          actionType: a.actionType ?? null,
          target: typeof a.target === "string" ? a.target : "",
          amount: Number.isFinite(a.amount) ? a.amount : 0,
          destination: typeof a.destination === "string" ? a.destination : "",
          hp: existing?.hp ?? 100,
          maxHp: existing?.maxHp ?? 100,
          moveMs: 0,
          opacity: existing?.opacity ?? 1,
          fadeMs: 0,
          isDying: false,
        }, a);

        if (!canOccupyAt(unitsByName, unitName, candidate.x, candidate.y, candidate)) continue;

        unitsByName.set(unitName, candidate);
        result.push({
          units: cloneMap(unitsByName),
          log: { text: `${unitName} appears at ${candidate.coord || "?"}`, color: candidate.team === "A" ? "#93c5fd" : "#fca5a5" },
          stepMs,
        });
        continue;
      }

      if (!existing) continue;

      if (actionKey === "moves") {
        const cell = parseDestination(a.destination ?? a.to ?? a.dest ?? a.position ?? a.cell ?? null);
        if (!cell) continue;
        if (cell.x < playableMinX || cell.x >= playableMaxX || cell.y < playableMinY || cell.y >= playableMaxY) continue;
        if (isRiver(cell.x, cell.y)) continue;

        const candidate = syncFields({
          ...existing,
          spriteIndex: baseSpriteIndex,
          footprintW: baseFootprintW,
          footprintH: baseFootprintH,
          x: cell.x,
          y: cell.y,
          coord: cellToCoord(cell.x, cell.y) ?? existing.coord,
          moveMs: stepMs,
        }, a);

        if (!canOccupyAt(unitsByName, unitName, candidate.x, candidate.y, candidate)) continue;

        const dx = candidate.x - existing.x;
        const nextDirection = dx < 0 ? "left" : dx > 0 ? "right" : existing.direction;
        unitsByName.set(unitName, { ...candidate, direction: nextDirection });
        result.push({
          units: cloneMap(unitsByName),
          log: { text: `${unitName} moves to ${candidate.coord || "?"}`, color: existing.team === "A" ? "#93c5fd" : "#fca5a5" },
          stepMs,
        });
        continue;
      }

      if (actionKey === "attacks") {
        unitsByName.set(unitName, { ...syncFields(existing, a), moveMs: 0 });
        const target = typeof a.target === "string" ? a.target : "?";
        result.push({
          units: cloneMap(unitsByName),
          log: { text: `${unitName} attacks ${target}`, color: existing.team === "A" ? "#93c5fd" : "#fca5a5" },
          stepMs,
        });
        continue;
      }

      if (actionKey === "looseshealth") {
        const delta = Number.isFinite(a.amount) ? Math.abs(a.amount) : 0;
        const maxHp = Number.isFinite(existing.maxHp) ? existing.maxHp : 100;
        const hpNow = Number.isFinite(existing.hp) ? existing.hp : maxHp;
        const nextHp = Math.max(0, hpNow - delta);

        unitsByName.set(unitName, {
          ...syncFields(existing, a),
          hp: nextHp,
          maxHp,
          moveMs: 0,
          opacity: 1,
          fadeMs: 0,
          isDying: false,
        });
        result.push({
          units: cloneMap(unitsByName),
          log: { text: `${unitName} takes ${delta} damage (${nextHp}/${maxHp} HP)`, color: existing.team === "A" ? "#93c5fd" : "#fca5a5" },
          stepMs,
        });
        continue;
      }

      if (actionKey === "dies") {
        unitsByName.delete(unitName);
        result.push({
          units: cloneMap(unitsByName),
          log: { text: `${unitName} dies`, color: existing.team === "A" ? "#93c5fd" : "#fca5a5" },
          stepMs,
        });
        continue;
      }

      // Unknown action type
      unitsByName.set(unitName, { ...syncFields(existing, a), moveMs: 0 });
      result.push({
        units: cloneMap(unitsByName),
        log: null,
        stepMs,
      });
    }

    // Determine winner
    const remaining = [...unitsByName.values()];
    const remainingA = remaining.filter((u) => u.team === "A").length;
    const remainingB = remaining.filter((u) => u.team === "B").length;
    let winner = null;
    if (remainingA > 0 && remainingB === 0) winner = "A";
    else if (remainingB > 0 && remainingA === 0) winner = "B";
    else if (remainingA === 0 && remainingB === 0) winner = "Draw";

    return { snapshots: result, winner };
  }, [cols, gameStarted, pickSpriteIndexFromBackend, rows, spriteSheets, teamError, teamResponse, terrainLoaded]);

  // Start playback when snapshots become available
  useEffect(() => {
    if (computedSnapshots.length > 0) {
      setCurrentStep(0);
      setIsPlaying(true);
      setBattleLog([]);
      setBattleWinner(null);
    }
  }, [computedSnapshots]);

  // Auto-advance playback
  useEffect(() => {
    if (!isPlaying || computedSnapshots.length === 0) return;
    if (currentStep >= computedSnapshots.length - 1) {
      setIsPlaying(false);
      if (computedWinner) {
        setBattleWinner(computedWinner);
        const winColor = computedWinner === "A" ? "#3b82f6" : computedWinner === "B" ? "#ef4444" : "#fff";
        const winText = computedWinner === "A" ? "Team A wins!" : computedWinner === "B" ? "Team B wins!" : "Draw!";
        setBattleLog((prev) => [...prev, { id: prev.length, text: winText, color: winColor }]);
      }
      return;
    }

    const ms = (computedSnapshots[currentStep]?.stepMs || 250) / playbackSpeed;
    const timer = setTimeout(() => {
      setCurrentStep((s) => Math.min(s + 1, computedSnapshots.length - 1));
    }, ms);

    return () => clearTimeout(timer);
  }, [isPlaying, currentStep, computedSnapshots, playbackSpeed, computedWinner]);

  // Apply current snapshot to units and log
  useEffect(() => {
    if (currentStep < 0 || !computedSnapshots[currentStep]) return;
    const snap = computedSnapshots[currentStep];
    const playing = isPlayingRef.current;

    // When stepping manually, disable movement transitions
    if (playing) {
      setUnits(snap.units);
    } else {
      setUnits(snap.units.map((u) => ({ ...u, moveMs: 0, fadeMs: 0 })));
    }

    // Build log up to current step
    const logs = [];
    for (let i = 0; i <= currentStep; i++) {
      const s = computedSnapshots[i];
      if (s.log) logs.push({ id: i, text: s.log.text, color: s.log.color });
    }
    setBattleLog(logs);

    // Winner display
    if (currentStep === computedSnapshots.length - 1 && computedWinner) {
      setBattleWinner(computedWinner);
    } else {
      setBattleWinner(null);
    }
  }, [currentStep, computedSnapshots, computedWinner]);

  // Playback control handlers
  const handlePlayPause = useCallback(() => {
    if (!isPlaying && currentStep >= computedSnapshots.length - 1) {
      // Restart from beginning if at end
      setCurrentStep(0);
      setIsPlaying(true);
    } else {
      setIsPlaying((p) => !p);
    }
  }, [isPlaying, currentStep, computedSnapshots.length]);

  const handleStepForward = useCallback(() => {
    setIsPlaying(false);
    setCurrentStep((s) => Math.min(s + 1, computedSnapshots.length - 1));
  }, [computedSnapshots.length]);

  const handleStepBackward = useCallback(() => {
    setIsPlaying(false);
    setCurrentStep((s) => Math.max(s - 1, 0));
  }, []);

  const handleSkipToStart = useCallback(() => {
    setIsPlaying(false);
    setCurrentStep(0);
  }, []);

  const handleSkipToEnd = useCallback(() => {
    setIsPlaying(false);
    setCurrentStep(computedSnapshots.length - 1);
  }, [computedSnapshots.length]);

  const winnerLabel = useMemo(() => {
    if (!battleWinner) return null;
    if (battleWinner === "A") return "Team A wins";
    if (battleWinner === "B") return "Team B wins";
    if (battleWinner === "Draw") return "Draw";
    return null;
  }, [battleWinner]);

  return (
    <>
    <div className="fixed inset-0 z-0 overflow-hidden flex items-center justify-center">
      <div
        className="absolute inset-0 bg-center bg-cover bg-no-repeat blur-md scale-105"
        style={{ backgroundImage: `url(${blurredBackgroundSrc})` }}
      />

      <div className="relative z-10" style={{ imageRendering: "pixelated" }}>
        {showWelcome ? (
          <div
            className={`fixed inset-0 z-40 flex items-center justify-center transition-opacity ${welcomeFading ? "opacity-0" : "opacity-100"}`}
            style={{ transitionDuration: `${welcomeFadeMs}ms` }}
          >
            <div
              className="fixed inset-0 bg-center bg-cover bg-no-repeat blur-sm"
              style={{ backgroundImage: `url(${titleScreenSrc})` }}
            />
            <div className="relative z-10 pointer-events-auto text-center bg-black/60 text-white px-6 py-6 rounded">
              <div className="text-3xl font-semibold">CSS Arena</div>
              <div className="mt-3 text-sm">натисніть Play щоб розпочати гру</div>
              <button
                type="button"
                className="mt-5 bg-white/90 text-black px-5 py-2 rounded cursor-pointer"
                onClick={() => {
                  setTeamError(null);
                  setTeamResponse(null);
                  setUnits([]);
                  setBattleLog([]);
                  setBattleWinner(null);
                  setPlayerTeam(null);
                  setPlayerTeamLabelVisible(false);
                  setPlayerTeamLabelOpaque(false);
                  setCurrentStep(-1);
                  setIsPlaying(false);
                  setGameStarted(true);
                  setWelcomeFading(true);
                  window.setTimeout(() => setShowWelcome(false), welcomeFadeMs);
                }}
              >
                Play
              </button>
            </div>
          </div>
        ) : null}

        {playerTeamLabelVisible && playerTeam === "A" ? (
          <div
            className={`absolute top-1/2 -translate-y-1/2 -mt-12 -left-3 -translate-x-full z-20 pointer-events-none text-sm bg-black/60 text-white px-3 py-2 rounded transition-opacity ${playerTeamLabelOpaque ? "opacity-100" : "opacity-0"}`}
            style={{ transitionDuration: `${playerTeamFadeMs}ms` }}
          >
            {`Ваша команда`}
          </div>
        ) : null}

        {playerTeamLabelVisible && playerTeam === "B" ? (
          <div
            className={`absolute top-1/2 -translate-y-1/2 -mt-12 -right-3 translate-x-full z-20 pointer-events-none text-sm bg-black/60 text-white px-3 py-2 rounded transition-opacity ${playerTeamLabelOpaque ? "opacity-100" : "opacity-0"}`}
            style={{ transitionDuration: `${playerTeamFadeMs}ms` }}
          >
            {`Ваша команда`}
          </div>
        ) : null}

        {winnerLabel ? (
          <div className="absolute top-3 left-1/2 -translate-x-1/2 z-30 pointer-events-auto text-sm bg-black/60 text-white px-3 py-2 rounded flex items-center gap-3">
            <span>{winnerLabel}</span>
            <button
              type="button"
              className="bg-white/90 text-black px-3 py-1 rounded cursor-pointer"
              onClick={() => window.location.reload()}
            >
              Restart
            </button>
          </div>
        ) : null}
        <img
          src={centeredImageSrc}
          alt=""
          className="block max-w-none"
          width={terrainSize.w || undefined}
          height={terrainSize.h || undefined}
          onLoad={(e) => {
            setTerrainLoaded(true);
            setTerrainSize({
              w: e.currentTarget.naturalWidth,
              h: e.currentTarget.naturalHeight,
            });
          }}
        />

        <div className="absolute inset-0 pointer-events-none">
          {units.map((pos) => {
            const sprite = spriteSheets[pos.spriteIndex] ?? spriteSheets[0];
            const footprintW = Math.max(1, Math.floor(sprite.footprintW || pos.footprintW || 1));
            const footprintH = Math.max(1, Math.floor(sprite.footprintH || pos.footprintH || 1));
            const renderW = tileSize * footprintW;
            const renderH = tileSize * footprintH;
            return (
              <Character
                key={pos.id}
                src={sprite.src}
                x={pos.x}
                y={pos.y}
                tileSize={tileSize}
                centerOffsetX={footprintW / 2}
                centerOffsetY={footprintH / 2}
                direction={pos.direction}
                cycleSeconds={1.2}
                frames={sprite.frames}
                frameW={renderW}
                frameH={renderH}
                moveMs={pos.moveMs}
                opacity={Number.isFinite(pos.opacity) ? pos.opacity : 1}
                fadeMs={Number.isFinite(pos.fadeMs) ? pos.fadeMs : 0}
                team={pos.team}
                hp={pos.hp}
                maxHp={pos.maxHp}
                typeLabel={typeLabelBySpriteIndex[pos.spriteIndex]}
              />
            );
          })}
        </div>

        {/* team labels outside the arena */}
        <div className="absolute top-1/2 -translate-y-1/2 -left-3 -translate-x-full z-20 pointer-events-none text-sm bg-black/60 px-3 py-2 rounded" style={{ color: '#3b82f6' }}>
          Team A
        </div>
        <div className="absolute top-1/2 -translate-y-1/2 -right-3 translate-x-full z-20 pointer-events-none text-sm bg-black/60 px-3 py-2 rounded" style={{ color: '#ef4444' }}>
          Team B
        </div>

        {/* Legend */}
        {gameStarted && !showWelcome && (
          <div className="absolute -bottom-3 translate-y-full left-0 z-20 pointer-events-none text-white bg-black/60 px-3 py-2 rounded" style={{ fontSize: '10px', fontFamily: 'monospace' }}>
            <div style={{ marginBottom: '4px', fontWeight: 'bold', fontSize: '11px' }}>Unit Types</div>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px 14px' }}>
              {legendEntries.map((entry, i) => (
                <div key={i} style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                  <div style={{
                    width: '20px',
                    height: '20px',
                    backgroundImage: `url(${entry.src})`,
                    backgroundSize: `${20 * entry.frames}px 20px`,
                    backgroundRepeat: 'no-repeat',
                    backgroundPosition: '0px 0px',
                    imageRendering: 'pixelated',
                    animationName: entry.frames > 1 ? 'sprite-bg' : 'none',
                    animationDuration: '1.2s',
                    animationTimingFunction: `steps(${entry.frames})`,
                    animationIterationCount: 'infinite',
                    ['--sheetShift']: `${20 * entry.frames}px`,
                  }} />
                  <span>{entry.label}</span>
                </div>
              ))}
            </div>
            <div style={{ marginTop: '6px', display: 'flex', gap: '12px' }}>
              <span><span style={{ color: '#3b82f6' }}>{'\u2588'}</span> Team A (left)</span>
              <span><span style={{ color: '#ef4444' }}>{'\u2588'}</span> Team B (right)</span>
            </div>
          </div>
        )}

      </div>
    </div>

    {/* Playback controls — portaled to overlay to avoid overflow-hidden clipping */}
    {gameStarted && !showWelcome && computedSnapshots.length > 0 && createPortal(
      <div
        style={{
          position: 'fixed',
          bottom: '16px',
          left: '50%',
          transform: 'translateX(-50%)',
          zIndex: 50,
          display: 'flex',
          alignItems: 'center',
          gap: '6px',
          backgroundColor: 'rgba(0,0,0,0.8)',
          borderRadius: '8px',
          padding: '8px 16px',
          fontFamily: 'monospace',
          fontSize: '12px',
          color: '#fff',
          userSelect: 'none',
          pointerEvents: 'auto',
          backdropFilter: 'blur(8px)',
        }}
      >
        <button
          type="button"
          onClick={handleSkipToStart}
          title="Skip to start"
          style={controlBtnStyle}
        >
          {'\u23EE'}
        </button>
        <button
          type="button"
          onClick={handleStepBackward}
          title="Previous action"
          style={controlBtnStyle}
        >
          {'\u23EA'}
        </button>
        <button
          type="button"
          onClick={handlePlayPause}
          title={isPlaying ? "Pause" : "Play"}
          style={{ ...controlBtnStyle, fontSize: '16px', width: '32px' }}
        >
          {isPlaying ? '\u23F8' : '\u25B6'}
        </button>
        <button
          type="button"
          onClick={handleStepForward}
          title="Next action"
          style={controlBtnStyle}
        >
          {'\u23E9'}
        </button>
        <button
          type="button"
          onClick={handleSkipToEnd}
          title="Skip to end"
          style={controlBtnStyle}
        >
          {'\u23ED'}
        </button>

        <span style={{ margin: '0 6px', color: '#aaa', fontSize: '11px', minWidth: '70px', textAlign: 'center' }}>
          {currentStep + 1} / {computedSnapshots.length}
        </span>

        <select
          value={playbackSpeed}
          onChange={(e) => setPlaybackSpeed(Number(e.target.value))}
          title="Playback speed"
          style={{
            background: 'rgba(255,255,255,0.15)',
            border: '1px solid rgba(255,255,255,0.3)',
            borderRadius: '4px',
            color: '#fff',
            fontSize: '11px',
            padding: '2px 4px',
            cursor: 'pointer',
            outline: 'none',
          }}
        >
          <option value={0.25}>0.25x</option>
          <option value={0.5}>0.5x</option>
          <option value={1}>1x</option>
          <option value={2}>2x</option>
          <option value={4}>4x</option>
          <option value={8}>8x</option>
        </select>
      </div>,
      document.getElementById('overlay-root')
    )}

    {createPortal(
      <div
        ref={logRef}
        style={{
          position: 'absolute',
          right: '16px',
          top: '16px',
          bottom: '16px',
          width: '260px',
          backgroundColor: 'rgba(0,0,0,0.75)',
          color: '#fff',
          borderRadius: '6px',
          overflowY: 'auto',
          fontSize: '11px',
          fontFamily: 'monospace',
          padding: '8px',
          scrollbarWidth: 'thin',
          pointerEvents: 'auto',
        }}
      >
        <div style={{ fontWeight: 'bold', fontSize: '12px', marginBottom: '6px', borderBottom: '1px solid rgba(255,255,255,0.2)', paddingBottom: '4px' }}>
          Battle Log
        </div>
        {battleLog.length === 0 && (
          <div style={{ color: '#888' }}>Waiting for battle...</div>
        )}
        {battleLog.map((entry) => (
          <div key={entry.id} style={{ color: entry.color || '#ccc', marginBottom: '2px', lineHeight: '1.4' }}>
            {entry.text}
          </div>
        ))}
      </div>,
      document.getElementById('overlay-root')
    )}
    </>
  );
};

const controlBtnStyle = {
  background: 'rgba(255,255,255,0.1)',
  border: '1px solid rgba(255,255,255,0.25)',
  borderRadius: '4px',
  color: '#fff',
  fontSize: '14px',
  width: '28px',
  height: '28px',
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  cursor: 'pointer',
  padding: 0,
  lineHeight: 1,
};

export default Arena;
