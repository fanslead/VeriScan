import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { OneTimeKeyDialog } from './OneTimeKeyDialog';

describe('OneTimeKeyDialog', () => {
  it('在确认已保存前不能关闭，并支持复制一次性 Key', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    const writeText = vi.spyOn(navigator.clipboard, 'writeText');
    render(
      <OneTimeKeyDialog
        visible
        payload={{
          key: {
            id: 'key-1',
            applicationId: 'app-1',
            name: '生产服务',
            prefix: 'vsk_live_abcd',
            status: 'active',
            createdAt: '2026-09-01T00:00:00Z',
            expiresAt: '2027-09-01T00:00:00Z',
            lastUsedAt: null,
            createdBy: '当前用户',
          },
          plaintext: 'vsk_live_a-secret-that-is-only-shown-once',
        }}
        onClose={onClose}
      />,
    );

    const done = screen.getByRole('button', { name: '我已安全保存' });
    expect(done).toBeDisabled();
    await user.click(screen.getByRole('button', { name: '复制 Key' }));
    expect(writeText).toHaveBeenCalledWith('vsk_live_a-secret-that-is-only-shown-once');
    expect(onClose).not.toHaveBeenCalled();

    await user.click(screen.getByRole('checkbox', { name: '我已将完整 Key 保存到安全位置' }));
    expect(done).toBeEnabled();
    await user.click(done);
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
