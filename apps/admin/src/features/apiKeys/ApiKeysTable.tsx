import { Button, Table, Tag } from '@douyinfe/semi-ui';
import { IconDelete, IconKey, IconRefresh } from '@douyinfe/semi-icons';
import type { ApiKey, ApiKeyStatus } from '@/shared/api/types';
import { formatDate } from '@/shared/ui/formatDate';

interface ApiKeysTableProps {
  rows: ApiKey[];
  canManage: boolean;
  onRotate: (key: ApiKey) => void;
  onRevoke: (key: ApiKey) => void;
}

export function ApiKeysTable({ rows, canManage, onRotate, onRevoke }: ApiKeysTableProps) {
  const columns = [
    {
      title: '凭证',
      dataIndex: 'name',
      width: 290,
      render: (_: unknown, record: ApiKey) => (
        <div className="key-table-identity">
          <span className="key-table-icon">
            <IconKey />
          </span>
          <span>
            <strong>{record.name}</strong>
            <code>{record.prefix}••••••••</code>
          </span>
        </div>
      ),
    },
    {
      title: '状态',
      dataIndex: 'status',
      width: 110,
      render: (value: ApiKeyStatus) => (
        <Tag
          color={value === 'active' ? 'green' : value === 'revoked' ? 'red' : 'grey'}
          size="small"
        >
          {value === 'active' ? '有效' : value === 'revoked' ? '已撤销' : '已过期'}
        </Tag>
      ),
    },
    {
      title: '创建时间',
      dataIndex: 'createdAt',
      render: (value: string) => (
        <span className="table-secondary">
          <strong>
            {formatDate(value, { year: 'numeric', month: 'numeric', day: 'numeric' })}
          </strong>
          <small>{formatDate(value, { hour: '2-digit', minute: '2-digit' })}</small>
        </span>
      ),
    },
    {
      title: '有效期',
      dataIndex: 'expiresAt',
      render: (value: string) => (
        <span className="table-secondary">
          <strong>
            {formatDate(value, { year: 'numeric', month: 'numeric', day: 'numeric' })}
          </strong>
          <small>{value ? '自动到期' : '暂无期限'}</small>
        </span>
      ),
    },
    {
      title: '最近使用',
      dataIndex: 'lastUsedAt',
      render: (value: string | null) => (
        <span className="data-mono">
          {value
            ? formatDate(value, {
                month: 'numeric',
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
              })
            : '尚未使用'}
        </span>
      ),
    },
    {
      title: '操作',
      dataIndex: 'id',
      align: 'right' as const,
      render: (_: unknown, record: ApiKey) =>
        record.status === 'active' && canManage ? (
          <div className="table-actions">
            <Button
              theme="borderless"
              type="tertiary"
              icon={<IconRefresh />}
              onClick={() => onRotate(record)}
            >
              轮换
            </Button>
            <Button
              theme="borderless"
              type="danger"
              icon={<IconDelete />}
              onClick={() => onRevoke(record)}
            >
              撤销
            </Button>
          </div>
        ) : (
          <span className="table-muted">无可用操作</span>
        ),
    },
  ];

  return (
    <Table
      className="data-table"
      columns={columns}
      dataSource={rows}
      rowKey="id"
      pagination={false}
    />
  );
}
