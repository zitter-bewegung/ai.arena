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
  const [playbackPanelHidden, setPlaybackPanelHidden] = useState(false);
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
    // Skip API call if teamResponse is already set (e.g. loaded from a replay file)
    if (teamResponse) return;
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
    const playableMaxX = cols;
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

    const centerCol = playableMinX + Math.floor(playableCols / 2);

    const isOutOfBounds = (x, y) =>
      x < playableMinX || x >= playableMaxX || y < playableMinY || y >= playableMaxY;

    const inTeamAZone = (x) => x < centerCol;
    const inTeamBZone = (x) => x >= centerCol;

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
        if (team === "A") x = randomInt(minX, Math.min(maxX, centerCol - 1));
        if (team === "B") x = randomInt(Math.max(minX, centerCol), maxX);

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
          hp: 10,
          maxHp: 10,
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

    // Unit stats keyed by backend UnitType string (JsonStringEnumConverter)
    const unitStatsByType = {
      light:      { movement: 13, range: 1 },
      heavy:      { movement: 5,  range: 1 },
      fast:       { movement: 15, range: 1 },
      shortrange: { movement: 6,  range: 3 },
      longrange:  { movement: 3,  range: 6 },
    };
    const getUnitStats = (unit) => {
      const t = unit?.unitType;
      if (t == null) return null;
      const key = String(t).toLowerCase().replace(/[^a-z]/g, '');
      return unitStatsByType[key] || null;
    };

    const rawActions =
      teamResponse.actions || teamResponse.battle?.actions || teamResponse.result?.actions || [];
    if (!Array.isArray(rawActions) || rawActions.length === 0) {
      return { snapshots: [], winner: null };
    }

    const playableMinX = 1;
    const playableMinY = 0;
    const playableMaxX = cols;
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
    let lastAttack = null; // { attacker, target } — used to detect counterattacks

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
          hp: existing?.hp ?? 10,
          maxHp: existing?.maxHp ?? 10,
          moveMs: 0,
          opacity: existing?.opacity ?? 1,
          fadeMs: 0,
          isDying: false,
        }, a);

        if (!canOccupyAt(unitsByName, unitName, candidate.x, candidate.y, candidate)) continue;

        unitsByName.set(unitName, candidate);
        result.push({
          units: cloneMap(unitsByName),
          log: { text: `[SPAWN] ${unitName} appears at ${candidate.coord || "?"}`, color: candidate.team === "A" ? "#93c5fd" : "#fca5a5" },
          stepMs,
          action: { type: 'appears', unitName },
        });
        continue;
      }

      if (!existing) continue;

      if (actionKey === "moves") {
        const cell = parseDestination(a.destination ?? a.to ?? a.dest ?? a.position ?? a.cell ?? null);
        if (!cell) continue;
        if (cell.x < playableMinX || cell.x >= playableMaxX || cell.y < playableMinY || cell.y >= playableMaxY) continue;

        const prevX = existing.x;
        const prevY = existing.y;

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
        const dy = candidate.y - existing.y;
        const moveDist = Math.round(Math.sqrt(dx * dx + dy * dy) * 10) / 10;
        const nextDirection = dx < 0 ? "left" : dx > 0 ? "right" : existing.direction;
        const fromCoord = cellToCoord(existing.x, existing.y) || "?";
        const moveTypeLabel = typeLabelBySpriteIndex[existing.spriteIndex] || '';
        const moveStats = getUnitStats(existing);
        const maxMove = moveStats ? moveStats.movement : '?';
        unitsByName.set(unitName, { ...candidate, direction: nextDirection });
        result.push({
          units: cloneMap(unitsByName),
          log: { text: `[MOVE]  ${unitName}${moveTypeLabel ? ` (${moveTypeLabel})` : ''} ${fromCoord} -> ${candidate.coord || "?"} (${moveDist}/${maxMove})`, color: existing.team === "A" ? "#93c5fd" : "#fca5a5" },
          stepMs,
          action: { type: 'moves', unitName, prevX, prevY, toX: cell.x, toY: cell.y, team: existing.team },
        });
        continue;
      }

      if (actionKey === "attacks") {
        unitsByName.set(unitName, { ...syncFields(existing, a), moveMs: 0 });
        const targetName = typeof a.target === "string" ? a.target : null;
        const targetUnit = targetName ? unitsByName.get(targetName) : null;
        const atkDist = targetUnit
          ? Math.round(Math.sqrt((existing.x - targetUnit.x) ** 2 + (existing.y - targetUnit.y) ** 2) * 10) / 10
          : '?';
        const atkTypeLabel = typeLabelBySpriteIndex[existing.spriteIndex] || '';
        const tgtTypeLabel = targetUnit ? (typeLabelBySpriteIndex[targetUnit.spriteIndex] || '') : '';
        const atkStats = getUnitStats(existing);
        const maxRange = atkStats ? atkStats.range : '?';
        const isCounter = lastAttack && lastAttack.attacker === targetName && lastAttack.target === unitName;
        const tag = isCounter ? '[CTR]' : '[ATK]';
        const label = isCounter
          ? `${tag}   ${unitName}${atkTypeLabel ? ` (${atkTypeLabel})` : ''} << ${targetName || "?"}${tgtTypeLabel ? ` (${tgtTypeLabel})` : ''} (range ${atkDist}/${maxRange}) [half dmg]`
          : `${tag}   ${unitName}${atkTypeLabel ? ` (${atkTypeLabel})` : ''} >> ${targetName || "?"}${tgtTypeLabel ? ` (${tgtTypeLabel})` : ''} (range ${atkDist}/${maxRange})`;
        lastAttack = { attacker: unitName, target: targetName };
        result.push({
          units: cloneMap(unitsByName),
          log: { text: label, color: existing.team === "A" ? "#93c5fd" : "#fca5a5" },
          stepMs,
          action: {
            type: 'attacks',
            unitName,
            targetName,
            attackerX: existing.x, attackerY: existing.y,
            attackerFootprintW: existing.footprintW, attackerFootprintH: existing.footprintH,
            targetX: targetUnit?.x, targetY: targetUnit?.y,
            targetFootprintW: targetUnit?.footprintW, targetFootprintH: targetUnit?.footprintH,
            team: existing.team,
          },
        });
        continue;
      }

      if (actionKey === "looseshealth") {
        const delta = Number.isFinite(a.amount) ? Math.abs(a.amount) : 0;
        const maxHp = Number.isFinite(existing.maxHp) ? existing.maxHp : 10;
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
          log: { text: `[DMG]   ${unitName} -${delta} HP (${nextHp}/${maxHp})`, color: existing.team === "A" ? "#93c5fd" : "#fca5a5" },
          stepMs,
          action: { type: 'looseshealth', unitName, delta, team: existing.team },
        });
        continue;
      }

      if (actionKey === "dies") {
        const dyingTeam = existing.team;
        unitsByName.delete(unitName);
        result.push({
          units: cloneMap(unitsByName),
          log: { text: `[KILL]  ${unitName} destroyed!`, color: dyingTeam === "A" ? "#93c5fd" : "#fca5a5" },
          stepMs,
          action: { type: 'dies', unitName, team: dyingTeam },
        });
        continue;
      }

      // Unknown action type
      unitsByName.set(unitName, { ...syncFields(existing, a), moveMs: 0 });
      result.push({
        units: cloneMap(unitsByName),
        log: null,
        stepMs,
        action: null,
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
  }, [cols, gameStarted, pickSpriteIndexFromBackend, rows, spriteSheets, teamError, teamResponse, terrainLoaded, typeLabelBySpriteIndex]);

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

  const handleStepBackward10 = useCallback(() => {
    setIsPlaying(false);
    setCurrentStep((s) => Math.max(s - 10, 0));
  }, []);

  const handleStepForward10 = useCallback(() => {
    setIsPlaying(false);
    setCurrentStep((s) => Math.min(s + 10, computedSnapshots.length - 1));
  }, [computedSnapshots.length]);

  const handleSkipToStart = useCallback(() => {
    setIsPlaying(false);
    setCurrentStep(0);
  }, []);

  const handleSkipToEnd = useCallback(() => {
    setIsPlaying(false);
    setCurrentStep(computedSnapshots.length - 1);
  }, [computedSnapshots.length]);

  // Build turn index: group snapshots into logical turns (one per unit action start)
  const turnEntries = useMemo(() => {
    if (computedSnapshots.length === 0) return [];
    const entries = [];
    let turnNum = 1;
    for (let i = 0; i < computedSnapshots.length; i++) {
      const action = computedSnapshots[i].action;
      if (!action) continue;
      const { type, unitName } = action;
      if (type === 'moves' || type === 'attacks' || type === 'appears') {
        const log = computedSnapshots[i].log;
        const isCounter = log?.text?.startsWith('[CTR]');
        if (!isCounter) {
          entries.push({
            stepIndex: i,
            label: `${turnNum}. ${log?.text || `${unitName} ${type}`}`,
          });
          turnNum++;
        }
      }
    }
    return entries;
  }, [computedSnapshots]);

  // Find which turn entry the current step belongs to
  const currentTurnIndex = useMemo(() => {
    if (turnEntries.length === 0) return 0;
    let best = 0;
    for (let i = 0; i < turnEntries.length; i++) {
      if (turnEntries[i].stepIndex <= currentStep) best = i;
      else break;
    }
    return best;
  }, [turnEntries, currentStep]);

  // Current action metadata for overlays
  const currentAction = useMemo(() => {
    if (currentStep < 0 || !computedSnapshots[currentStep]) return null;
    return computedSnapshots[currentStep].action || null;
  }, [currentStep, computedSnapshots]);

  // Highlight map: unitName -> 'attacking' | 'damaged'
  const highlightMap = useMemo(() => {
    const map = {};
    if (!currentAction) return map;
    if (currentAction.type === 'attacks') {
      if (currentAction.unitName) map[currentAction.unitName] = 'attacking';
      if (currentAction.targetName) map[currentAction.targetName] = 'damaged';
    }
    if (currentAction.type === 'looseshealth') {
      if (currentAction.unitName) map[currentAction.unitName] = 'damaged';
    }
    return map;
  }, [currentAction]);

  // Helper to convert grid coords to pixel center for SVG overlays
  const gridToPixelCenter = useCallback((gx, gy, fw = 1, fh = 1) => ({
    px: (gx + fw / 2) * tileSize,
    py: (gy + fh / 2) * tileSize,
  }), [tileSize]);

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
              <div className="mt-5 flex gap-3 justify-center">
                <button
                  type="button"
                  className="bg-white/90 text-black px-5 py-2 rounded cursor-pointer"
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
                <button
                  type="button"
                  className="bg-white/90 text-black px-5 py-2 rounded cursor-pointer"
                  onClick={() => {
                    const input = document.createElement("input");
                    input.type = "file";
                    input.accept = ".json";
                    input.onchange = (e) => {
                      const file = e.target.files?.[0];
                      if (!file) return;
                      const reader = new FileReader();
                      reader.onload = (ev) => {
                        try {
                          const data = JSON.parse(ev.target.result);
                          setTeamError(null);
                          setTeamResponse(data);
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
                        } catch (err) {
                          console.error("Failed to parse game log:", err);
                          setTeamError(err);
                        }
                      };
                      reader.readAsText(file);
                    };
                    input.click();
                  }}
                >
                  Load Replay
                </button>
              </div>
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
          {/* SVG overlay for movement trails and attack lines */}
          {terrainLoaded && currentAction && (
            <svg
              style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', pointerEvents: 'none', zIndex: 1 }}
              viewBox={`0 0 ${terrainSize.w} ${terrainSize.h}`}
            >
              <defs>
                <marker id="arrowhead-attack-a" markerWidth="8" markerHeight="6" refX="7" refY="3" orient="auto">
                  <polygon points="0 0, 8 3, 0 6" fill="#00eaff" />
                </marker>
                <marker id="arrowhead-attack-b" markerWidth="8" markerHeight="6" refX="7" refY="3" orient="auto">
                  <polygon points="0 0, 8 3, 0 6" fill="#ff2200" />
                </marker>
                <marker id="arrowhead-move-a" markerWidth="6" markerHeight="5" refX="5" refY="2.5" orient="auto">
                  <polygon points="0 0, 6 2.5, 0 5" fill="#00eaff" />
                </marker>
                <marker id="arrowhead-move-b" markerWidth="6" markerHeight="5" refX="5" refY="2.5" orient="auto">
                  <polygon points="0 0, 6 2.5, 0 5" fill="#ff6600" />
                </marker>
              </defs>

              {/* Movement trail */}
              {currentAction.type === 'moves' && Number.isFinite(currentAction.prevX) && Number.isFinite(currentAction.prevY) && (() => {
                const movingUnit = units.find(u => u.unitName === currentAction.unitName);
                const fw = movingUnit?.footprintW || 1;
                const fh = movingUnit?.footprintH || 1;
                const from = gridToPixelCenter(currentAction.prevX, currentAction.prevY, fw, fh);
                const to = gridToPixelCenter(currentAction.toX, currentAction.toY, fw, fh);
                const moveColor = currentAction.team === 'A' ? '#00eaff' : '#ff6600';
                const arrowId = currentAction.team === 'A' ? 'arrowhead-move-a' : 'arrowhead-move-b';
                return (
                  <>
                    <line
                      x1={from.px} y1={from.py} x2={to.px} y2={to.py}
                      stroke={moveColor} strokeWidth="2.5" strokeDasharray="6 4" opacity="0.9"
                      markerEnd={`url(#${arrowId})`}
                    />
                    <circle cx={from.px} cy={from.py} r="4" fill={moveColor} opacity="0.7" />
                  </>
                );
              })()}

              {/* Attack line */}
              {currentAction.type === 'attacks' && Number.isFinite(currentAction.attackerX) && Number.isFinite(currentAction.targetX) && (() => {
                const from = gridToPixelCenter(currentAction.attackerX, currentAction.attackerY, currentAction.attackerFootprintW || 1, currentAction.attackerFootprintH || 1);
                const to = gridToPixelCenter(currentAction.targetX, currentAction.targetY, currentAction.targetFootprintW || 1, currentAction.targetFootprintH || 1);
                const atkColor = currentAction.team === 'A' ? '#00eaff' : '#ff2200';
                const arrowId = currentAction.team === 'A' ? 'arrowhead-attack-a' : 'arrowhead-attack-b';
                return (
                  <>
                    <line
                      x1={from.px} y1={from.py} x2={to.px} y2={to.py}
                      stroke={atkColor} strokeWidth="3" opacity="1"
                      markerEnd={`url(#${arrowId})`}
                    />
                    {/* Impact burst at target */}
                    <circle cx={to.px} cy={to.py} r="8" fill="none" stroke={atkColor} strokeWidth="3" opacity="0.8" />
                    <circle cx={to.px} cy={to.py} r="14" fill="none" stroke={atkColor} strokeWidth="2" opacity="0.5" />
                  </>
                );
              })()}

              {/* Damage indicator */}
              {currentAction.type === 'looseshealth' && (() => {
                const damagedUnit = units.find(u => u.unitName === currentAction.unitName);
                if (!damagedUnit) return null;
                const pos = gridToPixelCenter(damagedUnit.x, damagedUnit.y, damagedUnit.footprintW || 1, damagedUnit.footprintH || 1);
                return (
                  <>
                    <circle cx={pos.px} cy={pos.py} r="10" fill="none" stroke="#ff4444" strokeWidth="2" opacity="0.6" />
                    <text
                      x={pos.px + 14} y={pos.py - 10}
                      fill="#ff4444" fontSize="12" fontFamily="monospace" fontWeight="bold"
                      style={{ textShadow: '0 0 4px rgba(0,0,0,0.8)' }}
                    >
                      -{currentAction.delta}
                    </text>
                  </>
                );
              })()}
            </svg>
          )}

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
                unitId={pos.unitName}
                highlight={highlightMap[pos.unitName] || null}
              />
            );
          })}
        </div>

        {/* team labels outside the arena */}
        {(() => {
          const meta = teamResponse?.metadata;
          let labelA = "Team A";
          let labelB = "Team B";
          if (meta) {
            if (meta.mode === "pvb") {
              // agentTeam tells us which team the checkpoint controls
              if (meta.agentTeam?.toLowerCase().includes("a")) {
                labelA = meta.checkpointA || "Agent";
                labelB = "Server Bot";
              } else {
                labelA = "Server Bot";
                labelB = meta.checkpointA || "Agent";
              }
            } else if (meta.mode === "pvp") {
              // teamA/teamB say which team name each client got
              if (meta.teamA?.toLowerCase().includes("a")) {
                labelA = meta.checkpointA || "Agent A";
                labelB = meta.checkpointB || "Agent B";
              } else {
                labelA = meta.checkpointB || "Agent B";
                labelB = meta.checkpointA || "Agent A";
              }
            }
          }
          // Strip .pt extension for cleaner display
          const clean = (s) => s.replace(/\.pt$/, "");
          return (
            <>
              <div className="absolute top-1/2 -translate-y-1/2 -left-3 -translate-x-full z-20 pointer-events-none text-sm bg-black/60 px-3 py-2 rounded text-center" style={{ color: '#3b82f6', maxWidth: '160px' }}>
                <div>Team A</div>
                {meta && <div style={{ fontSize: '10px', opacity: 0.7, marginTop: '2px' }}>{clean(labelA)}</div>}
              </div>
              <div className="absolute top-1/2 -translate-y-1/2 -right-3 translate-x-full z-20 pointer-events-none text-sm bg-black/60 px-3 py-2 rounded text-center" style={{ color: '#ef4444', maxWidth: '160px' }}>
                <div>Team B</div>
                {meta && <div style={{ fontSize: '10px', opacity: 0.7, marginTop: '2px' }}>{clean(labelB)}</div>}
              </div>
            </>
          );
        })()}

        {/* Legend — portaled so it's not clipped */}
        {gameStarted && !showWelcome && createPortal(
          <div style={{
            position: 'fixed',
            left: '16px',
            top: '16px',
            zIndex: 50,
            pointerEvents: 'none',
            color: '#fff',
            backgroundColor: 'rgba(0,0,0,0.65)',
            padding: '8px 12px',
            borderRadius: '6px',
            fontSize: '10px',
            fontFamily: 'monospace',
            backdropFilter: 'blur(8px)',
          }}>
            <div style={{ marginBottom: '4px', fontWeight: 'bold', fontSize: '11px' }}>Unit Types</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
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
            <div style={{ marginTop: '6px', display: 'flex', flexDirection: 'column', gap: '2px' }}>
              <span><span style={{ color: '#3b82f6' }}>{'\u2588'}</span> Team A (left)</span>
              <span><span style={{ color: '#ef4444' }}>{'\u2588'}</span> Team B (right)</span>
            </div>
          </div>,
          document.getElementById('overlay-root')
        )}

      </div>
    </div>

    {/* Playback controls + timeline — bottom-left */}
    {gameStarted && !showWelcome && computedSnapshots.length > 0 && createPortal(
      <div
        style={{
          position: 'fixed',
          bottom: '16px',
          left: '16px',
          zIndex: 50,
          pointerEvents: 'auto',
        }}
      >
        {playbackPanelHidden ? (
          <button
            type="button"
            onClick={() => setPlaybackPanelHidden(false)}
            title="Show playback controls"
            style={{
              backgroundColor: 'rgba(0,0,0,0.8)',
              backdropFilter: 'blur(8px)',
              border: '1px solid rgba(255,255,255,0.2)',
              borderRadius: '8px',
              color: '#fff',
              fontSize: '18px',
              width: '40px',
              height: '40px',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            {'▶'}
          </button>
        ) : (
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: '6px',
            backgroundColor: 'rgba(0,0,0,0.8)',
            borderRadius: '8px',
            padding: '10px 14px',
            fontFamily: 'monospace',
            fontSize: '12px',
            color: '#fff',
            userSelect: 'none',
            backdropFilter: 'blur(8px)',
            minWidth: '320px',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '-2px' }}>
            <button
              type="button"
              onClick={() => setPlaybackPanelHidden(true)}
              title="Hide playback controls"
              style={{
                background: 'none',
                border: 'none',
                color: '#888',
                fontSize: '14px',
                cursor: 'pointer',
                padding: '0 2px',
                lineHeight: 1,
              }}
            >
              {'✕'}
            </button>
          </div>
          {/* Playback buttons row */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
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
            onClick={handleStepBackward10}
            title="Back 10 steps"
            style={{ ...controlBtnStyle, fontSize: '10px', width: '32px' }}
          >
            -10
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
            onClick={handleStepForward10}
            title="Forward 10 steps"
            style={{ ...controlBtnStyle, fontSize: '10px', width: '32px' }}
          >
            +10
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
        </div>
        {/* Slider */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', width: '100%' }}>
          <span style={{ color: '#888', fontSize: '10px', minWidth: '14px' }}>1</span>
          <input
            type="range"
            min={0}
            max={computedSnapshots.length - 1}
            value={currentStep}
            onChange={(e) => {
              setIsPlaying(false);
              setCurrentStep(Number(e.target.value));
            }}
            style={{
              flex: 1,
              height: '4px',
              cursor: 'pointer',
              accentColor: '#3b82f6',
            }}
          />
          <span style={{ color: '#888', fontSize: '10px', minWidth: '14px' }}>{computedSnapshots.length}</span>
        </div>

        {/* Turn dropdown */}
        {turnEntries.length > 0 && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px', width: '100%' }}>
            <span style={{ color: '#aaa', fontSize: '10px', whiteSpace: 'nowrap' }}>Turn:</span>
            <select
              value={currentTurnIndex}
              onChange={(e) => {
                const idx = Number(e.target.value);
                const entry = turnEntries[idx];
                if (entry) {
                  setIsPlaying(false);
                  setCurrentStep(entry.stepIndex);
                }
              }}
              style={{
                flex: 1,
                background: 'rgba(255,255,255,0.12)',
                border: '1px solid rgba(255,255,255,0.25)',
                borderRadius: '4px',
                color: '#fff',
                fontSize: '10px',
                padding: '3px 4px',
                cursor: 'pointer',
                outline: 'none',
                maxWidth: '300px',
              }}
            >
              {turnEntries.map((entry, idx) => (
                <option key={idx} value={idx}>{entry.label}</option>
              ))}
            </select>
          </div>
        )}
        </div>
        )}
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
          width: '320px',
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
        {teamResponse?.metadata && (
          <div style={{ color: '#aaa', fontSize: '10px', marginBottom: '6px', borderBottom: '1px solid rgba(255,255,255,0.1)', paddingBottom: '4px' }}>
            <div>{teamResponse.metadata.mode === 'pvp' ? 'PvP' : 'PvB'} Replay</div>
            {teamResponse.metadata.checkpointA && <div>A: {teamResponse.metadata.checkpointA}</div>}
            {teamResponse.metadata.checkpointB && <div>B: {teamResponse.metadata.checkpointB}</div>}
          </div>
        )}
        {battleLog.length === 0 && (
          <div style={{ color: '#888' }}>Waiting for battle...</div>
        )}
        {battleLog.map((entry, idx) => {
          const isAttack = entry.text.startsWith('[ATK]');
          const isCounter = entry.text.startsWith('[CTR]');
          const isKill = entry.text.startsWith('[KILL]');
          const isDmg = entry.text.startsWith('[DMG]');
          const isSpawn = entry.text.startsWith('[SPAWN]');
          // Add a separator line before attack entries to group action sequences
          const prevEntry = idx > 0 ? battleLog[idx - 1] : null;
          const showSeparator = isAttack && prevEntry && !prevEntry.text.startsWith('[ATK]') && !prevEntry.text.startsWith('[CTR]');
          return (
            <div key={entry.id}>
              {showSeparator && (
                <div style={{ borderTop: '1px solid rgba(255,255,255,0.1)', margin: '4px 0' }} />
              )}
              <div style={{
                color: entry.color || '#ccc',
                marginBottom: '2px',
                lineHeight: '1.4',
                fontWeight: (isAttack || isKill) ? 'bold' : 'normal',
                fontStyle: isCounter ? 'italic' : 'normal',
                fontSize: isKill ? '12px' : '11px',
                opacity: isSpawn ? 0.7 : 1,
                paddingLeft: (isDmg || isCounter) ? '12px' : '0',
              }}>
                {entry.text}
              </div>
            </div>
          );
        })}
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
