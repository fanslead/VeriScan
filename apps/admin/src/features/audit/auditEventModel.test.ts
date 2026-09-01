import type { AuditEvent } from '@/shared/api/types';
import { getAuditActionLabel, getAuditChanges } from './auditEventModel';

const event: AuditEvent = {
  id: '1',
  tenantId: null,
  applicationId: null,
  apiKeyId: null,
  actorType: 'admin',
  actorId: 'operator',
  action: 'application.updated',
  resourceType: 'application',
  resourceId: 'app-1',
  beforeJson: JSON.stringify({ status: 'Active', applicationId: 'app-1' }),
  afterJson: JSON.stringify({ status: 'Suspended', applicationId: 'app-1' }),
  correlationId: null,
  occurredAt: '2026-09-01T00:00:00Z',
};

describe('auditEventModel', () => {
  it('将系统事件翻译为普通运营人员可读的变更', () => {
    expect(getAuditActionLabel(event.action)).toBe('更新应用');
    expect(getAuditChanges(event)).toEqual([{ label: '状态', before: '运行中', after: '已暂停' }]);
  });

  it('遇到损坏的摘要时安全降级为空变更', () => {
    expect(getAuditChanges({ ...event, beforeJson: '{', afterJson: null })).toEqual([]);
  });
});
