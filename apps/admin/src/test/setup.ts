import '@testing-library/jest-dom/vitest';
import '@douyinfe/semi-ui/lib/es/react19-adapter';

Object.assign(navigator, {
  clipboard: {
    writeText: vi.fn().mockResolvedValue(undefined),
  },
});

HTMLCanvasElement.prototype.getContext = (() => ({
  fillStyle: '',
  fillRect: vi.fn(),
})) as unknown as HTMLCanvasElement['getContext'];
