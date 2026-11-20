const KEY = 'medappLightDemo.coach';

export interface CoachPosition {
  step: number;
  mark: number;
  opened?: string;
}

let current: CoachPosition | null | undefined;
const listeners = new Set<() => void>();

function read(): CoachPosition | null {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return null;
    const value: unknown = JSON.parse(raw);
    if (value && typeof value === 'object' && Number.isInteger((value as CoachPosition).step) && Number.isInteger((value as CoachPosition).mark))
      return value as CoachPosition;
    return null;
  } catch {
    return null;
  }
}

export function coachPosition(): CoachPosition | null {
  if (current === undefined) current = read();
  return current;
}

function store(position: CoachPosition | null) {
  try {
    if (position) localStorage.setItem(KEY, JSON.stringify(position));
    else localStorage.removeItem(KEY);
  } catch {
    return;
  }
}

export function setCoachPosition(position: CoachPosition | null): void {
  current = position;
  store(position);
  listeners.forEach((listener) => listener());
}

export function subscribeCoach(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export const startCoachTour = (step = 0) => setCoachPosition({step, mark: 0});

export const endCoachTour = () => setCoachPosition(null);

export function resetCoachForTests(): void {
  current = undefined;
  listeners.clear();
}
