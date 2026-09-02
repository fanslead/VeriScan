import type {
  ApplicationWebhook,
  ApplicationWebhookSaved,
  ApplicationWebhookSecret,
  ApplicationWebhookTest,
  ApplicationWebhookTestAccepted,
  WebhookTestStatus,
} from './types';

type ObjectValue = Record<string, unknown>;

const invalidResponse = (): never => {
  throw new Error('响应数据无效，请稍后重试');
};

const asObject = (value: unknown): ObjectValue => {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return invalidResponse();
  }
  return value as ObjectValue;
};

const hasField = (object: ObjectValue, field: string): boolean =>
  Object.prototype.hasOwnProperty.call(object, field);

const requiredString = (object: ObjectValue, field: string): string => {
  if (!hasField(object, field) || typeof object[field] !== 'string') {
    return invalidResponse();
  }
  const value = object[field] as string;
  return value.trim() ? value : invalidResponse();
};

const nullableString = (object: ObjectValue, field: string): string | null => {
  if (!hasField(object, field)) return invalidResponse();
  const value = object[field];
  if (value === null) return null;
  if (typeof value !== 'string' || !value.trim()) return invalidResponse();
  return value;
};

const requiredDate = (object: ObjectValue, field: string): string => {
  const value = requiredString(object, field);
  return Number.isNaN(Date.parse(value)) ? invalidResponse() : value;
};

const nullableDate = (object: ObjectValue, field: string): string | null => {
  const value = nullableString(object, field);
  return value === null || !Number.isNaN(Date.parse(value)) ? value : invalidResponse();
};

const requiredBoolean = (object: ObjectValue, field: string): boolean => {
  if (!hasField(object, field) || typeof object[field] !== 'boolean') {
    return invalidResponse();
  }
  return object[field] as boolean;
};

const requiredNumber = (object: ObjectValue, field: string, integer = false): number => {
  if (!hasField(object, field) || typeof object[field] !== 'number') {
    return invalidResponse();
  }
  const value = object[field] as number;
  if (!Number.isFinite(value) || (integer && !Number.isInteger(value))) {
    return invalidResponse();
  }
  return value;
};

const nullableNumber = (object: ObjectValue, field: string): number | null => {
  if (!hasField(object, field)) return invalidResponse();
  const value = object[field];
  if (value === null) return null;
  if (typeof value !== 'number' || !Number.isFinite(value)) return invalidResponse();
  return value;
};

const nullableInteger = (object: ObjectValue, field: string): number | null => {
  const value = nullableNumber(object, field);
  return value === null || Number.isInteger(value) ? value : invalidResponse();
};

const nullableNonNegativeNumber = (object: ObjectValue, field: string): number | null => {
  const value = nullableNumber(object, field);
  return value === null || value >= 0 ? value : invalidResponse();
};

const webhookTestStatus = (object: ObjectValue, field: string): WebhookTestStatus => {
  const value = requiredString(object, field);
  if (
    value !== 'pending' &&
    value !== 'delivering' &&
    value !== 'succeeded' &&
    value !== 'failed'
  ) {
    return invalidResponse();
  }
  return value;
};

const nullableWebhookTestStatus = (
  object: ObjectValue,
  field: string,
): WebhookTestStatus | null => {
  if (!hasField(object, field)) return invalidResponse();
  if (object[field] === null) return null;
  return webhookTestStatus(object, field);
};

export function mapApplicationWebhookResponse(value: unknown): ApplicationWebhook {
  const object = asObject(value);
  const configured = requiredBoolean(object, 'configured');
  const webhook: ApplicationWebhook = {
    configured,
    id: nullableString(object, 'id'),
    applicationId: requiredString(object, 'applicationId'),
    endpointUrl: nullableString(object, 'endpointUrl'),
    enabled: requiredBoolean(object, 'enabled'),
    revision: nullableInteger(object, 'revision'),
    currentRevisionTested: requiredBoolean(object, 'currentRevisionTested'),
    lastTestId: nullableString(object, 'lastTestId'),
    lastTestStatus: nullableWebhookTestStatus(object, 'lastTestStatus'),
    lastTestHttpStatusCode: nullableInteger(object, 'lastTestHttpStatusCode'),
    lastTestLatencyMilliseconds: nullableNonNegativeNumber(object, 'lastTestLatencyMilliseconds'),
    lastTestedAt: nullableDate(object, 'lastTestedAt'),
    updatedAt: nullableDate(object, 'updatedAt'),
  };

  if (!configured) {
    if (
      webhook.id !== null ||
      webhook.endpointUrl !== null ||
      webhook.enabled ||
      webhook.revision !== null ||
      webhook.currentRevisionTested
    ) {
      return invalidResponse();
    }
  } else if (
    webhook.id === null ||
    webhook.endpointUrl === null ||
    webhook.revision === null ||
    webhook.revision < 1
  ) {
    return invalidResponse();
  }

  return webhook;
}

export function mapApplicationWebhookSavedResponse(value: unknown): ApplicationWebhookSaved {
  const object = asObject(value);
  if (!hasField(object, 'webhook')) return invalidResponse();
  return {
    webhook: mapApplicationWebhookResponse(object.webhook),
    signingSecret: nullableString(object, 'signingSecret'),
  };
}

export function mapApplicationWebhookSecretResponse(value: unknown): ApplicationWebhookSecret {
  const object = asObject(value);
  return {
    signingSecret: requiredString(object, 'signingSecret'),
    rotatedAt: requiredDate(object, 'rotatedAt'),
  };
}

export function mapApplicationWebhookTestAcceptedResponse(
  value: unknown,
): ApplicationWebhookTestAccepted {
  const object = asObject(value);
  return {
    testId: requiredString(object, 'testId'),
    statusUrl: requiredString(object, 'statusUrl'),
    submittedAt: requiredDate(object, 'submittedAt'),
  };
}

export function mapApplicationWebhookTestResponse(value: unknown): ApplicationWebhookTest {
  const object = asObject(value);
  const status = webhookTestStatus(object, 'status');
  const response: ApplicationWebhookTest = {
    testId: requiredString(object, 'testId'),
    applicationId: requiredString(object, 'applicationId'),
    configurationRevision: requiredNumber(object, 'configurationRevision', true),
    status,
    httpStatusCode: nullableInteger(object, 'httpStatusCode'),
    latencyMilliseconds: nullableNonNegativeNumber(object, 'latencyMilliseconds'),
    failureCode: nullableString(object, 'failureCode'),
    submittedAt: requiredDate(object, 'submittedAt'),
    completedAt: nullableDate(object, 'completedAt'),
  };

  if (response.configurationRevision < 1) return invalidResponse();
  if (status === 'succeeded' && response.completedAt === null) return invalidResponse();
  if (status === 'failed' && response.completedAt === null) return invalidResponse();
  return response;
}
