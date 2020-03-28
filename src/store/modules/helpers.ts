import * as fs from 'fs-extra';
export function formatDuration(ms: number) {
    if (!ms) return;
    if (ms < 0) ms = -ms;
    const time = {
        day: Math.floor(ms / 86400000),
        hour: Math.floor(ms / 3600000) % 24,
        minute: Math.floor(ms / 60000) % 60,
        second: Math.floor(ms / 1000) % 60,
        millisecond: Math.floor(ms) % 1000
    };
    return Object.entries(time)
        .filter(val => val[1] !== 0)
        .map(([key, val]) => `val key${val !== 1 ? 's' : ''}`)
        .join(', ');
}

interface TrackProgressOptions {
    file: string,
    size?: number,
    totalSize: number,
    listener(percentage: number): void
}
export function TrackProgress({ file, size = 0, totalSize, listener }: TrackProgressOptions) {
    const intId = setInterval(async () => {
        try {
            if (typeof totalSize === 'number' && totalSize > 0) {
                const fileInfo = await fs.stat(file);
                const percentage = (fileInfo.size + size) / totalSize;
                listener(percentage);
            } else {
                listener(404);
            }
        }
        catch{

        }
    }, 400);

    return {
        close() {
            clearInterval(intId);
        }
    };
}
