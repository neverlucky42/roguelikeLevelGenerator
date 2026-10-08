import React, {
    useEffect,
    useDeferredValue,
    useMemo,
    useState,
} from 'react';
import { createRoot } from 'react-dom/client';
import htm from 'htm';
import {
    Role,
    RoleBitByKey,
    RoleColors,
    RoleNames,
    RolePriority,
    SurvivalRoleKeys,
    computeCellCounts,
    defaultConfig,
    generateRoom,
    presets,
} from './generator.mjs';

const html = htm.bind(React.createElement);

const roleBits = [
    Role.Corridor,
    Role.Maneuver,
    Role.Spawn,
    Role.Loot,
    Role.Npc,
    Role.Structure,
];

const presetNames = Object.keys(presets);

function primaryRole(mask) {
    for (const role of RolePriority) {
        if (mask & role) return role;
    }
    return Role.None;
}

function popcount(mask) {
    return roleBits.reduce((count, role) => count + ((mask & role) ? 1 : 0), 0);
}

function maskToLabel(mask) {
    const labels = roleBits
        .filter((role) => mask & role)
        .map((role) => RoleNames[role]);
    return labels.length === 0 ? 'Free' : labels.join(' | ');
}

function pointMarker(mask) {
    if (mask & Role.Structure) return 'B';
    if (mask & Role.Spawn) return 'S';
    if (mask & Role.Loot) return 'L';
    if (mask & Role.Npc) return 'N';
    return null;
}

function cellClassNames(mask, locked, door) {
    const names = [];
    if (door) names.push('door');
    if (locked) names.push('locked');
    if (mask !== Role.None) names.push('reserved');
    if (popcount(mask) > 1) names.push('multi');
    for (const role of RolePriority) {
        if (mask & role) names.push(RoleNames[role].toLowerCase());
    }
    return names.join(' ');
}

function Field({ label, children, hint }) {
    return html`
        <div className="field">
            <div className="field-label">
                <span>${label}</span>
                ${hint ? html`<em>${hint}</em>` : null}
            </div>
            ${children}
        </div>
    `;
}

function RangeField({ label, value, min, max, step, onChange, hint }) {
    return html`
        <${Field} label=${label} hint=${hint}>
            <div className="range-row">
                <input
                    type="range"
                    min=${min}
                    max=${max}
                    step=${step}
                    value=${value}
                    onInput=${(event) => onChange(Number(event.target.value))}
                />
                <strong>${value}</strong>
            </div>
        <//>
    `;
}

function NumberField({ label, value, min, max, step, onChange, hint }) {
    return html`
        <${Field} label=${label} hint=${hint}>
            <input
                type="number"
                min=${min}
                max=${max}
                step=${step}
                value=${value}
                onInput=${(event) => onChange(Number(event.target.value))}
            />
        <//>
    `;
}

function SelectField({ label, value, options, onChange, hint }) {
    return html`
        <${Field} label=${label} hint=${hint}>
            <select value=${value} onChange=${(event) => onChange(event.target.value)}>
                ${options.map((option) => html`
                    <option key=${option.value} value=${option.value}>${option.label}</option>
                `)}
            </select>
        <//>
    `;
}

function ToggleField({ label, value, onChange, hint }) {
    return html`
        <${Field} label=${label} hint=${hint}>
            <button
                className=${value ? 'toggle active' : 'toggle'}
                onClick=${() => onChange(!value)}
            >
                ${value ? 'On' : 'Off'}
            </button>
        <//>
    `;
}

function Section({ title, children }) {
    return html`
        <details open>
            <summary>${title}</summary>
            <div className="section-body">${children}</div>
        </details>
    `;
}

function StatChip({ label, value, tone = 'default' }) {
    return html`
        <div className=${`stat ${tone}`}>
            <span>${label}</span>
            <strong>${value}</strong>
        </div>
    `;
}

function PointRules({ title, config, pointKey, updatePoint }) {
    const rule = config[pointKey];
    return html`
        <div className="point-rules">
            <h4>${title}</h4>
            <${RangeField}
                label="Количество"
                value=${rule.count}
                min=${0}
                max=${16}
                step=${1}
                onChange=${(value) => updatePoint(pointKey, 'count', value)}
            />
            <${RangeField}
                label="Мин. дистанция до двери"
                value=${rule.minDoorDistance}
                min=${0}
                max=${32}
                step=${1}
                onChange=${(value) => updatePoint(pointKey, 'minDoorDistance', value)}
            />
            <${RangeField}
                label="Мин. расстояние"
                value=${rule.minSpacing}
                min=${0}
                max=${32}
                step=${1}
                onChange=${(value) => updatePoint(pointKey, 'minSpacing', value)}
            />
            <${RangeField}
                label="Мин. поддержка"
                value=${rule.minSupport}
                min=${0}
                max=${32}
                step=${1}
                onChange=${(value) => updatePoint(pointKey, 'minSupport', value)}
            />
        </div>
    `;
}

function SurvivalRulesEditor({ config, selectedRole, onSelectRole, onChange }) {
    const roleLabel = (roleKey) => RoleNames[RoleBitByKey[roleKey]];
    const rules = config.survivalRules[selectedRole];

    return html`
        <div className="survival-rules">
            <div className="survival-tabs">
                ${SurvivalRoleKeys.map((roleKey) => html`
                    <button
                        key=${roleKey}
                        className=${selectedRole === roleKey ? 'active' : ''}
                        onClick=${() => onSelectRole(roleKey)}
                    >
                        ${roleLabel(roleKey)}
                    </button>
                `)}
            </div>
            <p className="survival-hint">
                Клетка <strong>${roleLabel(selectedRole)}</strong> выживает,
                если количество соседей каждого типа попадает в заданный диапазон.
            </p>
            <div className="survival-grid">
                ${SurvivalRoleKeys.map((neighborRole) => {
                    const rule = rules[neighborRole];
                    return html`
                        <div key=${neighborRole} className="survival-row">
                            <span>${roleLabel(neighborRole)}</span>
                            <input
                                type="number"
                                min=${0}
                                max=${32}
                                step=${1}
                                value=${rule.min}
                                onInput=${(event) => onChange(selectedRole, neighborRole, 'min', Number(event.target.value))}
                            />
                            <span>—</span>
                            <input
                                type="number"
                                min=${0}
                                max=${32}
                                step=${1}
                                value=${rule.max}
                                onInput=${(event) => onChange(selectedRole, neighborRole, 'max', Number(event.target.value))}
                            />
                        </div>
                    `;
                })}
            </div>
        </div>
    `;
}

function App() {
    const [config, setConfig] = useState(() => presets.boss);
    const [frame, setFrame] = useState(0);
    const [hovered, setHovered] = useState(null);
    const [importText, setImportText] = useState('');
    const [cellSize, setCellSize] = useState(20);
    const [showLocked, setShowLocked] = useState(true);
    const [survivalRole, setSurvivalRole] = useState('corridor');

    const deferredConfig = useDeferredValue(config);
    const result = useMemo(() => generateRoom(deferredConfig), [deferredConfig]);

    useEffect(() => {
        setFrame(result.snapshots.length - 1);
    }, [result]);

    const activeFrame = Math.min(frame, result.snapshots.length - 1);
    const activeMask = result.snapshots[activeFrame] || result.mask;
    const activeCounts = useMemo(
        () => computeCellCounts(activeMask, result.width, result.height, result.config),
        [activeMask, result.width, result.height, result.config],
    );

    const doorIndexes = useMemo(
        () => new Set(result.doors.map((door) => door.y * result.width + door.x)),
        [result.doors, result.width],
    );

    const update = (key, value) => {
        setConfig((current) => ({ ...current, [key]: value }));
    };

    const updatePoint = (pointKey, key, value) => {
        setConfig((current) => ({
            ...current,
            [pointKey]: {
                ...current[pointKey],
                [key]: value,
            },
        }));
    };

    const updateSurvivalRule = (targetRole, neighborRole, bound, value) => {
        setConfig((current) => ({
            ...current,
            survivalRules: {
                ...current.survivalRules,
                [targetRole]: {
                    ...current.survivalRules[targetRole],
                    [neighborRole]: {
                        ...current.survivalRules[targetRole][neighborRole],
                        [bound]: value,
                    },
                },
            },
        }));
    };

    const applyPreset = (name) => {
        setConfig({ ...presets[name] });
    };

    const randomizeSeed = () => {
        update('seed', Math.floor(Math.random() * 4294967295) + 1);
    };

    const exportJson = useMemo(() => JSON.stringify(config, null, 2), [config]);

    const copyJson = async () => {
        try {
            await navigator.clipboard.writeText(exportJson);
        } catch {
            const area = document.createElement('textarea');
            area.value = exportJson;
            document.body.appendChild(area);
            area.select();
            document.execCommand('copy');
            area.remove();
        }
    };

    const applyJson = () => {
        try {
            const parsed = JSON.parse(importText);
            setConfig({ ...defaultConfig(), ...parsed });
            setImportText('');
        } catch {
            window.alert('Invalid JSON');
        }
    };

    const gridStyle = {
        gridTemplateColumns: `repeat(${result.width}, var(--cell-size))`,
        gridTemplateRows: `repeat(${result.height}, var(--cell-size))`,
    };

    const hoveredPoint = hovered === null ? null : {
        x: hovered % result.width,
        y: Math.floor(hovered / result.width),
    };
    const hoveredMask = hovered === null ? Role.None : activeMask[hovered];
    const hoveredLocked = hovered === null ? false : Boolean(result.lockedMask[hovered]);
    const hoveredCounts = hovered === null ? null : activeCounts[hovered];

    const cells = useMemo(() => {
        const items = [];
        for (let i = 0; i < activeMask.length; i += 1) {
            const point = {
                x: i % result.width,
                y: Math.floor(i / result.width),
            };
            const mask = activeMask[i];
            const locked = result.lockedMask[i] !== Role.None;
            const door = doorIndexes.has(i);
            const counts = activeCounts[i];
            const label = maskToLabel(mask);
            const visualLocked = showLocked && locked;
            const marker = pointMarker(mask);
            items.push({
                index: i,
                point,
                mask,
                locked,
                door,
                counts,
                label,
                className: cellClassNames(mask, visualLocked, door),
                backgroundColor: RoleColors[primaryRole(mask)],
                title: `${point.x}, ${point.y} — ${door ? 'Door | ' : ''}${label} — C:${counts.corridor} M:${counts.maneuver} S:${counts.spawn} L:${counts.loot} N:${counts.npc} B:${counts.structure} R:${counts.reserved}${locked ? ' [locked]' : ''}`,
                marker,
            });
        }
        return items;
    }, [activeMask, activeCounts, doorIndexes, result.lockedMask, result.width, showLocked]);

    return html`
        <div className="app">
            <aside className="panel">
                <header>
                    <h1>Лаборатория Reserved-правил</h1>
                    <p>Клеточный автомат для зарезервированных клеток</p>
                </header>

                <div className="preset-row">
                    ${presetNames.map((name) => html`
                        <button
                            key=${name}
                            className=${config.roomType === name ? 'active' : ''}
                            onClick=${() => applyPreset(name)}
                        >
                            ${name}
                        </button>
                    `)}
                </div>

                <${Section} title="Комната">
                    <${RangeField}
                        label="Ширина"
                        value=${config.width}
                        min=${8}
                        max=${64}
                        step=${1}
                        onChange=${(value) => update('width', value)}
                    />
                    <${RangeField}
                        label="Высота"
                        value=${config.height}
                        min=${8}
                        max=${64}
                        step=${1}
                        onChange=${(value) => update('height', value)}
                    />
                    <${NumberField}
                        label="Сид"
                        value=${config.seed}
                        min=${1}
                        max=${4294967295}
                        step=${1}
                        onChange=${(value) => update('seed', value)}
                    />
                    <button className="wide" onClick=${randomizeSeed}>Random seed</button>
                    <${RangeField}
                        label="Двери"
                        value=${config.doorCount}
                        min=${1}
                        max=${8}
                        step=${1}
                        onChange=${(value) => update('doorCount', value)}
                    />
                    <${SelectField}
                        label="Режим пути"
                        value=${config.pathMode}
                        options=${[
                            { value: 'direct', label: 'Direct' },
                            { value: 'segment', label: 'Segment' },
                            { value: 'meander', label: 'Meander' },
                        ]}
                        onChange=${(value) => update('pathMode', value)}
                    />
                    <${RangeField}
                        label="Ширина коридора"
                        value=${config.corridorWidth}
                        min=${1}
                        max=${5}
                        step=${1}
                        onChange=${(value) => update('corridorWidth', value)}
                    />
                    <${RangeField}
                        label="Радиус хаба"
                        value=${config.hubRadius}
                        min=${0}
                        max=${12}
                        step=${1}
                        onChange=${(value) => update('hubRadius', value)}
                    />
                    <${RangeField}
                        label="Футпринт структуры"
                        value=${config.structureFootprint}
                        min=${0}
                        max=${5}
                        step=${1}
                        onChange=${(value) => update('structureFootprint', value)}
                    />
                    <${RangeField}
                        label="Начальный шум"
                        value=${config.noise}
                        min=${0}
                        max=${0.5}
                        step=${0.01}
                        onChange=${(value) => update('noise', value)}
                    />
                <//>

                <${Section} title="Автомат">
                    <${RangeField}
                        label="Итерации"
                        value=${config.iterations}
                        min=${0}
                        max=${40}
                        step=${1}
                        onChange=${(value) => update('iterations', value)}
                    />
                    <${SelectField}
                        label="Окрестность"
                        value=${config.neighborhood}
                        options=${[
                            { value: 'moore', label: 'Moore (8)' },
                            { value: 'vonNeumann', label: 'Von Neumann (4)' },
                        ]}
                        onChange=${(value) => update('neighborhood', value)}
                    />
                    <${RangeField}
                        label="Радиус окрестности"
                        value=${config.radius}
                        min=${1}
                        max=${4}
                        step=${1}
                        onChange=${(value) => update('radius', value)}
                    />
                    <${SelectField}
                        label="Источник рождения"
                        value=${config.birthSource}
                        options=${[
                            { value: 'any', label: 'Any reserved' },
                            { value: 'corridor', label: 'Corridor' },
                            { value: 'maneuver', label: 'Maneuver' },
                        ]}
                        onChange=${(value) => update('birthSource', value)}
                    />
                    <${RangeField}
                        label="Порог рождения"
                        value=${config.birthThreshold}
                        min=${0}
                        max=${32}
                        step=${1}
                        onChange=${(value) => update('birthThreshold', value)}
                        hint="Free -> Maneuver"
                    />
                    <${RangeField}
                        label="Кольцо коридора"
                        value=${config.corridorRing}
                        min=${0}
                        max=${32}
                        step=${1}
                        onChange=${(value) => update('corridorRing', value)}
                        hint="Maneuver neighbors around corridor"
                    />
                    <${RangeField}
                        label="Макс. доля Reserved"
                        value=${config.maxReservedRatio}
                        min=${0.05}
                        max=${1}
                        step=${0.01}
                        onChange=${(value) => update('maxReservedRatio', value)}
                    />
                    <${ToggleField}
                        label="Обрезать острова"
                        value=${config.pruneIslands}
                        onChange=${(value) => update('pruneIslands', value)}
                    />
                <//>

                <${Section} title="Правила выживания">
                    <${SurvivalRulesEditor}
                        config=${config}
                        selectedRole=${survivalRole}
                        onSelectRole=${setSurvivalRole}
                        onChange=${updateSurvivalRule}
                    />
                <//>

                <${Section} title="Точки">
                    <${PointRules}
                        title="Спавнеры"
                        config=${config}
                        pointKey="spawn"
                        updatePoint=${updatePoint}
                    />
                    <${PointRules}
                        title="Лут"
                        config=${config}
                        pointKey="loot"
                        updatePoint=${updatePoint}
                    />
                    <${PointRules}
                        title="NPC"
                        config=${config}
                        pointKey="npc"
                        updatePoint=${updatePoint}
                    />
                <//>

                <${Section} title="JSON правил">
                    <button className="wide" onClick=${copyJson}>Скопировать JSON</button>
                    <pre>${exportJson}</pre>
                    <textarea
                        placeholder="Вставь JSON правил"
                        value=${importText}
                        onInput=${(event) => setImportText(event.target.value)}
                    />
                    <button className="wide" onClick=${applyJson}>Применить JSON</button>
                <//>
            </aside>

            <main className="workspace">
                <div className="toolbar">
                    <${StatChip} label="Комната" value=${`${result.width}×${result.height}`} />
                    <${StatChip} label="Reserved" value=${`${(result.stats.reservedRatio * 100).toFixed(1)}%`} />
                    <${StatChip} label="Corridor" value=${result.roleCounts[Role.Corridor]} />
                    <${StatChip} label="Maneuver" value=${result.roleCounts[Role.Maneuver]} />
                    <${StatChip} label="Spawn" value=${result.roleCounts[Role.Spawn]} />
                    <${StatChip} label="Loot" value=${result.roleCounts[Role.Loot]} />
                    <${StatChip} label="NPC" value=${result.roleCounts[Role.Npc]} />
                    <${StatChip} label="Structure" value=${result.roleCounts[Role.Structure]} />
                    <${StatChip}
                        label="Связность"
                        value=${result.stats.connected ? 'yes' : 'no'}
                        tone=${result.stats.connected ? 'good' : 'bad'}
                    />
                </div>

                <div className="frame-row">
                    <span>
                        ${activeFrame === result.snapshots.length - 1
                            ? 'Финал'
                            : `Итерация ${activeFrame} / ${result.stats.iterations}`}
                    </span>
                    <input
                        type="range"
                        min=${0}
                        max=${result.snapshots.length - 1}
                        step=${1}
                        value=${activeFrame}
                        onInput=${(event) => setFrame(Number(event.target.value))}
                    />
                    <label>
                        <input
                            type="checkbox"
                            checked=${showLocked}
                            onChange=${(event) => setShowLocked(event.target.checked)}
                        />
                        Показывать фикс
                    </label>
                    <${RangeField}
                        label="Размер клетки"
                        value=${cellSize}
                        min=${8}
                        max=${36}
                        step=${1}
                        onChange=${setCellSize}
                    />
                </div>

                <div className="grid-wrap" style=${{ '--cell-size': `${cellSize}px` }}>
                    <div className="grid" style=${gridStyle}>
                        ${cells.map((cell) => html`
                            <div
                                key=${cell.index}
                                className=${cell.className}
                                style=${{ backgroundColor: cell.backgroundColor }}
                                title=${cell.title}
                                onMouseEnter=${() => setHovered(cell.index)}
                                onMouseLeave=${() => setHovered(null)}
                                onClick=${() => setHovered(cell.index)}
                            >
                                ${cell.door ? html`<span className="door-marker"></span>` : null}
                                ${cell.marker ? html`<span className="point-marker">${cell.marker}</span>` : null}
                            </div>
                        `)}
                    </div>
                </div>

                <div className="legend">
                    ${roleBits.map((role) => html`
                        <div key=${role} className="legend-item">
                            <span style=${{ backgroundColor: RoleColors[role] }}></span>
                            ${RoleNames[role]}
                        </div>
                    `)}
                    <div className="legend-item">
                        <span className="free-swatch"></span>
                        Free / obstacle candidate
                    </div>
                </div>

                <div className="inspector">
                    ${hoveredPoint ? html`
                        <div>
                            <strong>Cell</strong>
                            <span>${hoveredPoint.x}, ${hoveredPoint.y}</span>
                        </div>
                        <div>
                            <strong>Roles</strong>
                            <span>${maskToLabel(hoveredMask)}</span>
                        </div>
                        <div>
                            <strong>Locked</strong>
                            <span>${hoveredLocked ? 'yes' : 'no'}</span>
                        </div>
                        <div>
                            <strong>Neighbors</strong>
                            <span>
                                C ${hoveredCounts.corridor} /
                                M ${hoveredCounts.maneuver} /
                                S ${hoveredCounts.spawn} /
                                L ${hoveredCounts.loot} /
                                N ${hoveredCounts.npc} /
                                B ${hoveredCounts.structure} /
                                R ${hoveredCounts.reserved}
                            </span>
                        </div>
                    ` : html`
                        <div className="empty">Наведи на клетку, чтобы увидеть её локальное состояние</div>
                    `}
                </div>

                ${result.warnings.length > 0 ? html`
                    <div className="warnings">
                        ${result.warnings.map((warning) => html`<div key=${warning}>${warning}</div>`)}
                    </div>
                ` : null}
            </main>
        </div>
    `;
}

const container = document.getElementById('root');
createRoot(container).render(html`<${App} />`);
