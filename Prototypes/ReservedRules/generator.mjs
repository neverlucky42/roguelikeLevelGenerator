export const Role = Object.freeze({
    None: 0,
    Corridor: 1,
    Maneuver: 2,
    Spawn: 4,
    Loot: 8,
    Npc: 16,
    Structure: 32,
});

export const RoleNames = Object.freeze({
    [Role.None]: 'Free',
    [Role.Corridor]: 'Corridor',
    [Role.Maneuver]: 'Maneuver',
    [Role.Spawn]: 'Spawn',
    [Role.Loot]: 'Loot',
    [Role.Npc]: 'NPC',
    [Role.Structure]: 'Structure',
});

export const RoleColors = Object.freeze({
    [Role.None]: '#20242c',
    [Role.Corridor]: '#37b8d5',
    [Role.Maneuver]: '#63d67a',
    [Role.Spawn]: '#f39b3d',
    [Role.Loot]: '#f4d35e',
    [Role.Npc]: '#8b7cf6',
    [Role.Structure]: '#e45b93',
});

export const RolePriority = Object.freeze([
    Role.Structure,
    Role.Spawn,
    Role.Loot,
    Role.Npc,
    Role.Corridor,
    Role.Maneuver,
]);

export const SurvivalRoleKeys = Object.freeze([
    'corridor',
    'maneuver',
    'spawn',
    'loot',
    'npc',
    'structure',
]);

export const RoleBitByKey = Object.freeze({
    corridor: Role.Corridor,
    maneuver: Role.Maneuver,
    spawn: Role.Spawn,
    loot: Role.Loot,
    npc: Role.Npc,
    structure: Role.Structure,
});

const sideNames = Object.freeze(['top', 'right', 'bottom', 'left']);

class Rng {
    constructor(seed) {
        this.state = (seed >>> 0) || 1;
    }

    next() {
        let x = this.state;
        x ^= x << 13;
        x >>>= 0;
        x ^= x >> 17;
        x ^= x << 5;
        x >>>= 0;
        this.state = x;
        return x;
    }

    float() {
        return this.next() / 4294967296;
    }

    int(min, max) {
        if (max < min) [min, max] = [max, min];
        return min + Math.floor(this.float() * (max - min + 1));
    }

    chance(probability) {
        return this.float() < probability;
    }

    shuffle(items) {
        const result = [...items];
        for (let i = result.length - 1; i > 0; i -= 1) {
            const j = this.int(0, i);
            [result[i], result[j]] = [result[j], result[i]];
        }
        return result;
    }
}

function clampInt(value, min, max) {
    return Math.max(min, Math.min(max, Math.round(Number(value) || 0)));
}

function clampFloat(value, min, max) {
    return Math.max(min, Math.min(max, Number(value) || 0));
}

function pointToIndex(point, width) {
    return point.y * width + point.x;
}

function indexToPoint(index, width) {
    return {
        x: index % width,
        y: Math.floor(index / width),
    };
}

function isInside(point, width, height) {
    return point.x >= 0 && point.y >= 0 && point.x < width && point.y < height;
}

function copyMask(mask) {
    return Uint16Array.from(mask);
}

function sameMask(a, b) {
    if (a.length !== b.length) return false;
    for (let i = 0; i < a.length; i += 1) {
        if (a[i] !== b[i]) return false;
    }
    return true;
}

function neighborIndexes(point, width, height, radius, shape) {
    const result = [];
    const r = Math.max(0, Math.round(radius));
    for (let dy = -r; dy <= r; dy += 1) {
        for (let dx = -r; dx <= r; dx += 1) {
            if (dx === 0 && dy === 0) continue;
            if (shape === 'vonNeumann' && Math.abs(dx) + Math.abs(dy) > r) continue;
            const next = { x: point.x + dx, y: point.y + dy };
            if (!isInside(next, width, height)) continue;
            result.push(pointToIndex(next, width));
        }
    }
    return result;
}

export function computeCellCounts(mask, width, height, config) {
    const counts = new Array(mask.length);
    for (let i = 0; i < mask.length; i += 1) {
        const point = indexToPoint(i, width);
        const neighbors = neighborIndexes(point, width, height, config.radius, config.neighborhood);
        const roleCounts = {
            corridor: 0,
            maneuver: 0,
            spawn: 0,
            loot: 0,
            npc: 0,
            structure: 0,
        };
        let reserved = 0;
        for (const j of neighbors) {
            if (mask[j] & Role.Corridor) roleCounts.corridor += 1;
            if (mask[j] & Role.Maneuver) roleCounts.maneuver += 1;
            if (mask[j] & Role.Spawn) roleCounts.spawn += 1;
            if (mask[j] & Role.Loot) roleCounts.loot += 1;
            if (mask[j] & Role.Npc) roleCounts.npc += 1;
            if (mask[j] & Role.Structure) roleCounts.structure += 1;
            if (mask[j] !== Role.None) reserved += 1;
        }
        counts[i] = { ...roleCounts, reserved };
    }
    return counts;
}

function countRole(mask, index, role, width, height, config) {
    const point = indexToPoint(index, width);
    let count = 0;
    for (const j of neighborIndexes(point, width, height, config.radius, config.neighborhood)) {
        if (mask[j] & role) count += 1;
    }
    return count;
}

function countReserved(mask, index, width, height, config) {
    const point = indexToPoint(index, width);
    let count = 0;
    for (const j of neighborIndexes(point, width, height, config.radius, config.neighborhood)) {
        if (mask[j] !== Role.None) count += 1;
    }
    return count;
}

function markDisk(mask, lockedMask, center, radius, role, locked, width, height, overwrite = false) {
    const r = Math.max(0, Math.round(radius));
    for (let dy = -r; dy <= r; dy += 1) {
        for (let dx = -r; dx <= r; dx += 1) {
            if (dx * dx + dy * dy > r * r + r) continue;
            const point = { x: center.x + dx, y: center.y + dy };
            if (!isInside(point, width, height)) continue;
            const index = pointToIndex(point, width);
            if (overwrite) mask[index] = role;
            else mask[index] |= role;
            if (locked) lockedMask[index] |= role;
        }
    }
}

function markSquare(mask, lockedMask, center, size, role, locked, width, height) {
    const side = Math.max(1, Math.round(size));
    const start = -Math.floor((side - 1) / 2);
    for (let dy = start; dy < start + side; dy += 1) {
        for (let dx = start; dx < start + side; dx += 1) {
            const point = { x: center.x + dx, y: center.y + dy };
            if (!isInside(point, width, height)) continue;
            const index = pointToIndex(point, width);
            mask[index] |= role;
            if (locked) lockedMask[index] |= role;
        }
    }
}

function defaultSurvivalRules() {
    return {
        corridor: {
            corridor: { min: 1, max: 8 },
            maneuver: { min: 0, max: 8 },
            spawn: { min: 0, max: 2 },
            loot: { min: 0, max: 2 },
            npc: { min: 0, max: 2 },
            structure: { min: 0, max: 8 },
        },
        maneuver: {
            corridor: { min: 0, max: 8 },
            maneuver: { min: 1, max: 8 },
            spawn: { min: 0, max: 3 },
            loot: { min: 0, max: 3 },
            npc: { min: 0, max: 3 },
            structure: { min: 0, max: 8 },
        },
        spawn: {
            corridor: { min: 0, max: 4 },
            maneuver: { min: 1, max: 8 },
            spawn: { min: 0, max: 3 },
            loot: { min: 0, max: 2 },
            npc: { min: 0, max: 2 },
            structure: { min: 0, max: 8 },
        },
        loot: {
            corridor: { min: 0, max: 4 },
            maneuver: { min: 1, max: 8 },
            spawn: { min: 0, max: 1 },
            loot: { min: 0, max: 3 },
            npc: { min: 0, max: 2 },
            structure: { min: 0, max: 8 },
        },
        npc: {
            corridor: { min: 0, max: 4 },
            maneuver: { min: 1, max: 8 },
            spawn: { min: 0, max: 2 },
            loot: { min: 0, max: 2 },
            npc: { min: 0, max: 3 },
            structure: { min: 0, max: 8 },
        },
        structure: {
            corridor: { min: 0, max: 8 },
            maneuver: { min: 0, max: 8 },
            spawn: { min: 0, max: 8 },
            loot: { min: 0, max: 8 },
            npc: { min: 0, max: 8 },
            structure: { min: 0, max: 8 },
        },
    };
}

function deepMerge(base, override) {
    if (Array.isArray(base) || Array.isArray(override)) {
        return override === undefined ? base : override;
    }
    if (typeof base === 'object' && base !== null && typeof override === 'object' && override !== null) {
        const result = { ...base };
        for (const key of Object.keys(override)) {
            result[key] = deepMerge(base[key], override[key]);
        }
        return result;
    }
    return override === undefined ? base : override;
}

export function defaultConfig() {
    return {
        width: 28,
        height: 20,
        seed: 20261008,
        roomType: 'boss',
        doorCount: 2,
        pathMode: 'direct',
        corridorWidth: 1,
        hubRadius: 4,
        structureFootprint: 2,
        noise: 0.03,
        iterations: 10,
        neighborhood: 'moore',
        radius: 1,
        birthSource: 'any',
        birthThreshold: 2,
        corridorRing: 3,
        maxReservedRatio: 0.62,
        pruneIslands: true,
        survivalRules: defaultSurvivalRules(),
        spawn: {
            count: 5,
            minDoorDistance: 4,
            minSpacing: 4,
            minSupport: 3,
        },
        loot: {
            count: 1,
            minDoorDistance: 7,
            minSpacing: 8,
            minSupport: 4,
        },
        npc: {
            count: 1,
            minDoorDistance: 5,
            minSpacing: 8,
            minSupport: 4,
        },
    };
}

export const presets = {
    boss: {
        ...defaultConfig(),
        roomType: 'boss',
        width: 32,
        height: 24,
        hubRadius: 5,
        structureFootprint: 2,
        corridorRing: 3,
        birthThreshold: 2,
        maxReservedRatio: 0.68,
        survivalRules: {
            corridor: {
                spawn: { min: 0, max: 1 },
                loot: { min: 0, max: 1 },
                npc: { min: 0, max: 1 },
            },
            maneuver: {
                spawn: { min: 0, max: 3 },
            },
            spawn: {
                maneuver: { min: 2, max: 8 },
            },
            loot: {
                maneuver: { min: 3, max: 8 },
            },
            npc: {
                maneuver: { min: 2, max: 8 },
            },
        },
        spawn: { count: 6, minDoorDistance: 5, minSpacing: 4, minSupport: 3 },
        loot: { count: 1, minDoorDistance: 8, minSpacing: 8, minSupport: 4 },
        npc: { count: 0, minDoorDistance: 5, minSpacing: 8, minSupport: 4 },
    },
    normal: {
        ...defaultConfig(),
        roomType: 'normal',
        width: 26,
        height: 20,
        hubRadius: 3,
        structureFootprint: 0,
        corridorRing: 3,
        birthThreshold: 3,
        maxReservedRatio: 0.52,
        survivalRules: {
            corridor: {
                spawn: { min: 0, max: 2 },
                loot: { min: 0, max: 2 },
            },
            maneuver: {
                spawn: { min: 0, max: 4 },
            },
            spawn: {
                maneuver: { min: 2, max: 8 },
            },
            loot: {
                maneuver: { min: 3, max: 8 },
            },
        },
        spawn: { count: 3, minDoorDistance: 4, minSpacing: 5, minSupport: 3 },
        loot: { count: 1, minDoorDistance: 7, minSpacing: 8, minSupport: 4 },
        npc: { count: 0, minDoorDistance: 5, minSpacing: 8, minSupport: 4 },
    },
    treasure: {
        ...defaultConfig(),
        roomType: 'treasure',
        width: 22,
        height: 18,
        hubRadius: 3,
        structureFootprint: 0,
        corridorRing: 2,
        birthThreshold: 3,
        maxReservedRatio: 0.45,
        survivalRules: {
            corridor: {
                loot: { min: 0, max: 1 },
            },
            maneuver: {
                loot: { min: 1, max: 8 },
            },
            loot: {
                maneuver: { min: 3, max: 8 },
                spawn: { min: 0, max: 0 },
            },
        },
        spawn: { count: 1, minDoorDistance: 5, minSpacing: 5, minSupport: 3 },
        loot: { count: 2, minDoorDistance: 8, minSpacing: 7, minSupport: 4 },
        npc: { count: 0, minDoorDistance: 5, minSpacing: 8, minSupport: 4 },
    },
    shop: {
        ...defaultConfig(),
        roomType: 'shop',
        width: 22,
        height: 18,
        hubRadius: 3,
        structureFootprint: 0,
        corridorRing: 2,
        birthThreshold: 3,
        maxReservedRatio: 0.48,
        survivalRules: {
            corridor: {
                npc: { min: 0, max: 1 },
            },
            maneuver: {
                npc: { min: 1, max: 8 },
            },
            npc: {
                maneuver: { min: 2, max: 8 },
                spawn: { min: 0, max: 1 },
            },
        },
        spawn: { count: 0, minDoorDistance: 5, minSpacing: 5, minSupport: 3 },
        loot: { count: 0, minDoorDistance: 8, minSpacing: 8, minSupport: 4 },
        npc: { count: 2, minDoorDistance: 5, minSpacing: 6, minSupport: 4 },
    },
};

function normalizeConfig(config) {
    const base = defaultConfig();
    const merged = { ...base, ...config };
    merged.width = clampInt(merged.width, 8, 64);
    merged.height = clampInt(merged.height, 8, 64);
    merged.seed = (Number(merged.seed) || 1) >>> 0;
    merged.doorCount = clampInt(merged.doorCount, 1, 8);
    merged.corridorWidth = clampInt(merged.corridorWidth, 1, 5);
    merged.hubRadius = clampInt(merged.hubRadius, 0, 12);
    merged.structureFootprint = clampInt(merged.structureFootprint, 0, 5);
    merged.noise = clampFloat(merged.noise, 0, 0.5);
    merged.iterations = clampInt(merged.iterations, 0, 40);
    merged.radius = clampInt(merged.radius, 1, 4);
    merged.birthThreshold = clampInt(merged.birthThreshold, 0, 32);
    merged.corridorRing = clampInt(merged.corridorRing, 0, 32);
    merged.maxReservedRatio = clampFloat(merged.maxReservedRatio, 0.05, 1);
    merged.survivalRules = deepMerge(defaultSurvivalRules(), config.survivalRules || {});
    for (const targetRole of SurvivalRoleKeys) {
        const targetRules = merged.survivalRules[targetRole];
        for (const neighborRole of SurvivalRoleKeys) {
            const rule = targetRules[neighborRole];
            rule.min = clampInt(rule.min, 0, 32);
            rule.max = clampInt(rule.max, 0, 32);
            if (rule.min > rule.max) {
                [rule.min, rule.max] = [rule.max, rule.min];
            }
        }
    }
    for (const key of ['spawn', 'loot', 'npc']) {
        merged[key] = { ...base[key], ...(config[key] || {}) };
        merged[key].count = clampInt(merged[key].count, 0, 16);
        merged[key].minDoorDistance = clampInt(merged[key].minDoorDistance, 0, 32);
        merged[key].minSpacing = clampInt(merged[key].minSpacing, 0, 32);
        merged[key].minSupport = clampInt(merged[key].minSupport, 0, 32);
    }
    if (!['direct', 'segment', 'meander'].includes(merged.pathMode)) merged.pathMode = 'direct';
    if (!['moore', 'vonNeumann'].includes(merged.neighborhood)) merged.neighborhood = 'moore';
    if (!['corridor', 'maneuver', 'any'].includes(merged.birthSource)) merged.birthSource = 'any';
    return merged;
}

function generateDoors(config, rng) {
    const doors = [];
    const entrances = [];
    const sides = rng.shuffle(sideNames);
    const usedDoorKeys = new Set();
    for (let i = 0; i < config.doorCount; i += 1) {
        const side = sides[i % sides.length];
        let door;
        let entrance;
        if (side === 'top') {
            const x = rng.int(2, config.width - 3);
            door = { x, y: 0 };
            entrance = { x, y: 1 };
        } else if (side === 'bottom') {
            const x = rng.int(2, config.width - 3);
            door = { x, y: config.height - 1 };
            entrance = { x, y: config.height - 2 };
        } else if (side === 'left') {
            const y = rng.int(2, config.height - 3);
            door = { x: 0, y };
            entrance = { x: 1, y };
        } else {
            const y = rng.int(2, config.height - 3);
            door = { x: config.width - 1, y };
            entrance = { x: config.width - 2, y };
        }
        let guard = 0;
        while (usedDoorKeys.has(`${door.x}:${door.y}`) && guard < 100) {
            guard += 1;
            if (side === 'top' || side === 'bottom') {
                door.x = door.x >= config.width - 3 ? 2 : door.x + 1;
            } else {
                door.y = door.y >= config.height - 3 ? 2 : door.y + 1;
            }
        }
        if (side === 'top' || side === 'bottom') {
            entrance = { x: door.x, y: side === 'top' ? 1 : config.height - 2 };
        } else {
            entrance = { x: side === 'left' ? 1 : config.width - 2, y: door.y };
        }
        usedDoorKeys.add(`${door.x}:${door.y}`);
        doors.push(door);
        entrances.push(entrance);
    }
    return { doors, entrances, sides };
}

function chooseHub(config, rng) {
    const xJitter = Math.max(1, Math.floor(config.width / 10));
    const yJitter = Math.max(1, Math.floor(config.height / 10));
    const x = clampInt(
        Math.floor(config.width / 2) + rng.int(-xJitter, xJitter),
        2,
        config.width - 3,
    );
    const y = clampInt(
        Math.floor(config.height / 2) + rng.int(-yJitter, yJitter),
        2,
        config.height - 3,
    );
    return { x, y };
}

function straightPath(from, to) {
    const path = [];
    let x = from.x;
    let y = from.y;
    path.push({ x, y });
    while (x !== to.x || y !== to.y) {
        if (x !== to.x) x += Math.sign(to.x - x);
        else if (y !== to.y) y += Math.sign(to.y - y);
        path.push({ x, y });
    }
    return path;
}

function uniquePath(points) {
    const seen = new Set();
    const result = [];
    for (const point of points) {
        const key = `${point.x}:${point.y}`;
        if (seen.has(key)) continue;
        seen.add(key);
        result.push(point);
    }
    return result;
}

function tracePath(start, end, mode, rng) {
    if (mode === 'segment') {
        const midX = rng.int(Math.min(start.x, end.x), Math.max(start.x, end.x));
        const midY = rng.int(Math.min(start.y, end.y), Math.max(start.y, end.y));
        return uniquePath([
            ...straightPath(start, { x: midX, y: start.y }),
            ...straightPath({ x: midX, y: start.y }, { x: midX, y: midY }),
            ...straightPath({ x: midX, y: midY }, end),
        ]);
    }

    if (mode === 'meander') {
        const path = [{ ...start }];
        let x = start.x;
        let y = start.y;
        let guard = 0;
        while ((x !== end.x || y !== end.y) && guard < 10000) {
            guard += 1;
            const dx = Math.sign(end.x - x);
            const dy = Math.sign(end.y - y);
            if (dx !== 0 && dy !== 0) {
                if (rng.chance(0.55)) x += dx;
                else y += dy;
            } else if (dx !== 0) {
                x += dx;
            } else if (dy !== 0) {
                y += dy;
            } else {
                break;
            }
            path.push({ x, y });
        }
        return uniquePath(path);
    }

    return straightPath(start, end);
}

function perpendicularOffsets(width) {
    const size = Math.max(1, Math.round(width));
    const start = -Math.floor((size - 1) / 2);
    return Array.from({ length: size }, (_, i) => start + i);
}

function markPath(mask, lockedMask, path, width, height, corridorWidth) {
    const offsets = perpendicularOffsets(corridorWidth);
    for (let i = 0; i < path.length; i += 1) {
        const current = path[i];
        const previous = path[i - 1] || current;
        const next = path[i + 1] || current;
        const directionX = Math.sign(next.x - previous.x);
        const directionY = Math.sign(next.y - previous.y);

        const mark = (point) => {
            if (!isInside(point, width, height)) return;
            const index = pointToIndex(point, width);
            mask[index] |= Role.Corridor;
        };

        if (directionX !== 0) {
            for (const offset of offsets) mark({ x: current.x, y: current.y + offset });
        }
        if (directionY !== 0) {
            for (const offset of offsets) mark({ x: current.x + offset, y: current.y });
        }
        if (directionX === 0 && directionY === 0) mark(current);
    }
}

function supportCount(mask, index, config, width, height) {
    if (config.birthSource === 'corridor') {
        return countRole(mask, index, Role.Corridor, width, height, config);
    }
    if (config.birthSource === 'maneuver') {
        return countRole(mask, index, Role.Maneuver, width, height, config);
    }
    return countReserved(mask, index, width, height, config);
}

function neighborRoleCounts(mask, index, config, width, height) {
    const point = indexToPoint(index, width);
    const neighbors = neighborIndexes(point, width, height, config.radius, config.neighborhood);
    const counts = {
        corridor: 0,
        maneuver: 0,
        spawn: 0,
        loot: 0,
        npc: 0,
        structure: 0,
        reserved: 0,
    };
    for (const j of neighbors) {
        if (mask[j] & Role.Corridor) counts.corridor += 1;
        if (mask[j] & Role.Maneuver) counts.maneuver += 1;
        if (mask[j] & Role.Spawn) counts.spawn += 1;
        if (mask[j] & Role.Loot) counts.loot += 1;
        if (mask[j] & Role.Npc) counts.npc += 1;
        if (mask[j] & Role.Structure) counts.structure += 1;
        if (mask[j] !== Role.None) counts.reserved += 1;
    }
    return counts;
}

function survivesRole(mask, index, roleKey, config, width, height) {
    const counts = neighborRoleCounts(mask, index, config, width, height);
    const rules = config.survivalRules[roleKey];
    for (const neighborRole of SurvivalRoleKeys) {
        const rule = rules[neighborRole];
        const count = counts[neighborRole];
        if (count < rule.min || count > rule.max) {
            return false;
        }
    }
    return true;
}

function weakestUnlockedRole(mask, lockedMask, index, config, width, height) {
    const counts = neighborRoleCounts(mask, index, config, width, height);
    let weakestRole = null;
    let weakestSupport = Number.POSITIVE_INFINITY;
    for (const roleKey of SurvivalRoleKeys) {
        const roleBit = RoleBitByKey[roleKey];
        if (!(mask[index] & roleBit)) continue;
        if (lockedMask[index] & roleBit) continue;
        const support = counts[roleKey];
        if (support < weakestSupport) {
            weakestSupport = support;
            weakestRole = roleBit;
        }
    }
    return weakestRole;
}

function reservedRatio(mask) {
    let reserved = 0;
    for (const value of mask) {
        if (value !== Role.None) reserved += 1;
    }
    return mask.length === 0 ? 0 : reserved / mask.length;
}

function enforceCorridorRing(mask, lockedMask, config, width, height) {
    for (let i = 0; i < mask.length; i += 1) {
        if (!(mask[i] & Role.Corridor)) continue;
        let maneuverCount = countRole(mask, i, Role.Maneuver, width, height, config);
        if (maneuverCount >= config.corridorRing) continue;

        const point = indexToPoint(i, width);
        const neighbors = neighborIndexes(point, width, height, config.radius, config.neighborhood);
        for (const j of neighbors) {
            if (maneuverCount >= config.corridorRing) break;
            if (mask[j] !== Role.None) continue;
            if (lockedMask[j] & Role.Corridor) continue;
            mask[j] |= Role.Maneuver;
            maneuverCount += 1;
        }
    }
}

function enforceReservedRatio(mask, lockedMask, config, width, height, rng) {
    let ratio = reservedRatio(mask);
    if (ratio <= config.maxReservedRatio) return;

    const candidates = [];
    for (let i = 0; i < mask.length; i += 1) {
        if (mask[i] === Role.None) continue;
        if (mask[i] & Role.Corridor) continue;
        const weakestRole = weakestUnlockedRole(mask, lockedMask, i, config, width, height);
        if (weakestRole === null) continue;
        const support = neighborRoleCounts(mask, i, config, width, height);
        const roleKey = SurvivalRoleKeys.find((key) => RoleBitByKey[key] === weakestRole);
        candidates.push({
            index: i,
            role: weakestRole,
            support: support[roleKey],
            noise: rng.float(),
        });
    }
    candidates.sort((a, b) => (a.support - b.support) || (a.noise - b.noise));

    for (const candidate of candidates) {
        if (ratio <= config.maxReservedRatio) break;
        mask[candidate.index] &= ~candidate.role;
        ratio = reservedRatio(mask);
    }
}

function runAutomaton(initialMask, lockedMask, config, rng, width, height) {
    const mask = copyMask(initialMask);
    const snapshots = [copyMask(mask)];
    let previous = copyMask(mask);

    for (let iteration = 0; iteration < config.iterations; iteration += 1) {
        const next = copyMask(mask);

        for (let i = 0; i < next.length; i += 1) {
            for (const roleKey of SurvivalRoleKeys) {
                const roleBit = RoleBitByKey[roleKey];
                if (!(mask[i] & roleBit)) continue;
                if (lockedMask[i] & roleBit) continue;
                if (!survivesRole(mask, i, roleKey, config, width, height)) {
                    next[i] &= ~roleBit;
                }
            }

            if (next[i] === Role.None && (lockedMask[i] & Role.Maneuver) === 0) {
                const support = supportCount(mask, i, config, width, height);
                if (support >= config.birthThreshold) {
                    next[i] |= Role.Maneuver;
                }
            }
        }

        enforceCorridorRing(next, lockedMask, config, width, height);
        enforceReservedRatio(next, lockedMask, config, width, height, rng);

        mask.set(next);
        snapshots.push(copyMask(mask));

        if (sameMask(previous, mask)) break;
        previous = copyMask(mask);
    }

    return { mask, snapshots };
}

function floodFillReserved(mask, startIndex, config, width, height) {
    const visited = new Uint8Array(mask.length);
    if (mask[startIndex] === Role.None) return visited;
    const queue = [startIndex];
    visited[startIndex] = 1;

    while (queue.length > 0) {
        const index = queue.pop();
        const point = indexToPoint(index, width);
        for (const j of neighborIndexes(point, width, height, 1, 'moore')) {
            if (visited[j]) continue;
            if (mask[j] === Role.None) continue;
            visited[j] = 1;
            queue.push(j);
        }
    }
    return visited;
}

function pruneOrAuditIslands(mask, config, width, height, hubIndex, warnings) {
    const visited = floodFillReserved(mask, hubIndex, config, width, height);
    let disconnected = 0;
    for (let i = 0; i < mask.length; i += 1) {
        if (mask[i] === Role.None) continue;
        if (visited[i]) continue;
        disconnected += 1;
        if (config.pruneIslands) mask[i] = Role.None;
    }
    if (!config.pruneIslands && disconnected > 0) {
        warnings.push(`${disconnected} reserved cells are not connected to the hub`);
    }
    return { visited, disconnected };
}

function manhattan(a, b) {
    return Math.abs(a.x - b.x) + Math.abs(a.y - b.y);
}

function placePoints(mask, lockedMask, config, rng, doors, hub, width, height, warnings) {
    const points = {
        [Role.Spawn]: [],
        [Role.Loot]: [],
        [Role.Npc]: [],
    };
    const occupied = new Set();
    const definitions = [
        { role: Role.Spawn, key: 'spawn', label: 'spawn' },
        { role: Role.Loot, key: 'loot', label: 'loot' },
        { role: Role.Npc, key: 'npc', label: 'NPC' },
    ];

    for (const definition of definitions) {
        const rule = config[definition.key];
        let remaining = rule.count;
        let spacing = rule.minSpacing;
        let doorDistance = rule.minDoorDistance;
        let support = rule.minSupport;

        while (remaining > 0) {
            const candidates = [];
            for (let i = 0; i < mask.length; i += 1) {
                if (!(mask[i] & Role.Maneuver)) continue;
                if (mask[i] & Role.Corridor) continue;
                if (occupied.has(i)) continue;
                const point = indexToPoint(i, width);
                const nearestDoor = doors.reduce((minimum, door) => Math.min(minimum, manhattan(point, door)), Number.POSITIVE_INFINITY);
                if (nearestDoor < doorDistance) continue;
                const neighborSupport = countRole(mask, i, Role.Maneuver, width, height, config);
                if (neighborSupport < support) continue;
                if (points[definition.role].some((existing) => manhattan(point, existing) < spacing)) continue;
                candidates.push({
                    index: i,
                    point,
                    nearestDoor,
                    support: neighborSupport,
                    distanceToHub: manhattan(point, hub),
                    noise: rng.float(),
                });
            }

            if (candidates.length === 0) {
                if (spacing > 0) {
                    spacing -= 1;
                    continue;
                }
                if (doorDistance > 0) {
                    doorDistance -= 1;
                    continue;
                }
                if (support > 0) {
                    support -= 1;
                    continue;
                }
                warnings.push(`Could place only ${rule.count - remaining} of ${rule.count} ${definition.label} points`);
                break;
            }

            let best = candidates[0];
            let bestScore = Number.NEGATIVE_INFINITY;
            for (const candidate of candidates) {
                const spacingScore = points[definition.role].length === 0
                    ? 12
                    : Math.min(12, points[definition.role].reduce((minimum, existing) => Math.min(minimum, manhattan(candidate.point, existing)), Number.POSITIVE_INFINITY));
                const score = (
                    candidate.support * 2.5
                    + Math.min(candidate.nearestDoor, 12) * 1.8
                    + spacingScore * 2.2
                    + candidate.noise
                );
                if (score > bestScore) {
                    bestScore = score;
                    best = candidate;
                }
            }

            mask[best.index] |= definition.role;
            points[definition.role].push(best.point);
            occupied.add(best.index);
            remaining -= 1;
        }
    }

    return points;
}

function countRoles(mask) {
    const counts = {
        [Role.None]: 0,
        [Role.Corridor]: 0,
        [Role.Maneuver]: 0,
        [Role.Spawn]: 0,
        [Role.Loot]: 0,
        [Role.Npc]: 0,
        [Role.Structure]: 0,
    };
    for (const value of mask) {
        if (value === Role.None) counts[Role.None] += 1;
        if (value & Role.Corridor) counts[Role.Corridor] += 1;
        if (value & Role.Maneuver) counts[Role.Maneuver] += 1;
        if (value & Role.Spawn) counts[Role.Spawn] += 1;
        if (value & Role.Loot) counts[Role.Loot] += 1;
        if (value & Role.Npc) counts[Role.Npc] += 1;
        if (value & Role.Structure) counts[Role.Structure] += 1;
    }
    return counts;
}

export function generateRoom(inputConfig) {
    const config = normalizeConfig(inputConfig);
    const rng = new Rng(config.seed);
    const width = config.width;
    const height = config.height;
    const size = width * height;
    const mask = new Uint16Array(size);
    const lockedMask = new Uint16Array(size);
    const warnings = [];

    const { doors, entrances, sides } = generateDoors(config, rng);
    const hub = chooseHub(config, rng);

    for (const door of doors) {
        const index = pointToIndex(door, width);
        mask[index] |= Role.Corridor;
        lockedMask[index] |= Role.Corridor;
    }
    for (const entrance of entrances) {
        const index = pointToIndex(entrance, width);
        mask[index] |= Role.Corridor;
        lockedMask[index] |= Role.Corridor;
    }

    markDisk(mask, lockedMask, hub, config.hubRadius, Role.Maneuver, true, width, height);
    if (config.structureFootprint > 0) {
        markSquare(mask, lockedMask, hub, config.structureFootprint, Role.Structure, true, width, height);
    }

    for (const entrance of entrances) {
        const path = tracePath(entrance, hub, config.pathMode, rng);
        markPath(mask, lockedMask, path, width, height, config.corridorWidth);
    }

    const seededPoints = placePoints(
        mask,
        lockedMask,
        config,
        rng,
        doors,
        hub,
        width,
        height,
        warnings,
    );
    for (const role of [Role.Spawn, Role.Loot, Role.Npc]) {
        for (const point of seededPoints[role]) {
            const index = pointToIndex(point, width);
            mask[index] |= role | Role.Maneuver;
            lockedMask[index] |= role | Role.Maneuver;
        }
    }

    if (config.noise > 0) {
        for (let i = 0; i < mask.length; i += 1) {
            if (mask[i] !== Role.None) continue;
            if (lockedMask[i] !== Role.None) continue;
            if (rng.chance(config.noise)) mask[i] |= Role.Maneuver;
        }
    }

    const automaton = runAutomaton(mask, lockedMask, config, rng, width, height);
    mask.set(automaton.mask);

    const hubIndex = pointToIndex(hub, width);
    const connectivity = pruneOrAuditIslands(mask, config, width, height, hubIndex, warnings);
    const points = seededPoints;

    const snapshots = [...automaton.snapshots, copyMask(mask)];
    const counts = computeCellCounts(mask, width, height, config);
    const roleCounts = countRoles(mask);
    const reservedCount = size - roleCounts[Role.None];

    return {
        config,
        width,
        height,
        seed: config.seed,
        mask,
        lockedMask,
        doors,
        entrances,
        sides,
        hub,
        points,
        snapshots,
        counts,
        roleCounts,
        stats: {
            reservedCount,
            reservedRatio: reservedRatio(mask),
            iterations: automaton.snapshots.length - 1,
            connected: connectivity.disconnected === 0,
            disconnectedCount: connectivity.disconnected,
        },
        warnings,
    };
}
