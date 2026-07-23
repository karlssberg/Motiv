import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Catalog, EvaluationResult, RulesApiClient } from '@motiv/rules-core';
import { CheckoutPane } from '../../src/panes/CheckoutPane.js';

const eligibility: EvaluationResult = {
  satisfied: true,
  reason: 'customer is active',
  assertions: ['customer is active'],
  values: ['customer is active'],
  justification: 'customer is active',
  explanation: { assertions: ['customer is active'], underlying: [] },
};

const screening: EvaluationResult = {
  satisfied: true,
  reason: 'passes credit check',
  assertions: ['passes credit check'],
  values: ['passes credit check'],
  justification: 'passes credit check',
  explanation: { assertions: ['passes credit check'], underlying: [] },
};

const approval = { approved: true, eligibility, screening };

const catalog: Catalog = {
  specs: [],
  collections: [],
  metadataTypes: {},
  modelTypes: {
    customer: {
      type: ['object', 'null'],
      properties: {
        age: { type: 'integer' },
        isActive: { type: 'boolean' },
        orderCount: { type: 'integer' },
      },
    },
  },
};

const clientWith = (data: Catalog): RulesApiClient =>
  ({ getCatalog: vi.fn().mockResolvedValue(data) }) as unknown as RulesApiClient;

/** Flushes the mocked getCatalog resolution into the pane's state. */
const settleCatalog = () => act(async () => {});

describe('CheckoutPane', () => {
  afterEach(() => vi.restoreAllMocks());

  it('posts the sample customer to /api/checkout and renders both verdicts', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(JSON.stringify(approval), { status: 200 }));
    render(<CheckoutPane />);

    await userEvent.click(screen.getByRole('button', { name: /try checkout/i }));

    expect(fetchSpy).toHaveBeenCalledWith('/api/checkout', expect.objectContaining({ method: 'POST' }));
    expect(await screen.findByText('Approved')).toBeDefined();
    expect(screen.getByText('customer is active')).toBeDefined();
    expect(screen.getByText('passes credit check')).toBeDefined();
  });

  it('lets the user edit the sample customer JSON before trying', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(JSON.stringify({ ...approval, approved: false }), { status: 200 }));
    render(<CheckoutPane />);

    const input = screen.getByLabelText(/customer/i);
    await userEvent.clear(input);
    await userEvent.type(input, '{{"age": 15, "isActive": true, "orderCount": 0}');
    await userEvent.click(screen.getByRole('button', { name: /try checkout/i }));

    expect(await screen.findByText('Rejected')).toBeDefined();
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/checkout',
      expect.objectContaining({ body: '{"age": 15, "isActive": true, "orderCount": 0}' }),
    );
  });

  it('shows an error without assuming a JSON body when the server rejects the request', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 400 }));
    render(<CheckoutPane />);

    await userEvent.click(screen.getByRole('button', { name: /try checkout/i }));

    expect(await screen.findByRole('alert')).toBeDefined();
    expect(screen.getByText(/checkout failed \(400\)/i)).toBeDefined();
  });

  it('shows an error and clears the outcome when the request throws', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(JSON.stringify(approval), { status: 200 }));
    render(<CheckoutPane />);
    await userEvent.click(screen.getByRole('button', { name: /try checkout/i }));
    await screen.findByText('Approved');

    fetchSpy.mockRejectedValue(new Error('network down'));
    await userEvent.click(screen.getByRole('button', { name: /try checkout/i }));

    expect(await screen.findByRole('alert')).toBeDefined();
    expect(screen.getByText(/network down/)).toBeDefined();
    expect(screen.queryByText('Approved')).toBeNull();
  });

  it('blocks the checkout and shows the violations when the customer breaks the model schema', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch');
    render(<CheckoutPane client={clientWith(catalog)} />);
    await settleCatalog();

    fireEvent.change(screen.getByLabelText(/customer/i), {
      target: { value: '{ "age": "thirty", "isActive": true, "orderCount": 3 }' },
    });
    await userEvent.click(screen.getByRole('button', { name: /try checkout/i }));

    expect(await screen.findByText('$.age: expected integer, got string')).toBeDefined();
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('posts a schema-conforming customer and clears earlier violations', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(JSON.stringify(approval), { status: 200 }));
    render(<CheckoutPane client={clientWith(catalog)} />);
    await settleCatalog();

    const input = screen.getByLabelText(/customer/i);
    fireEvent.change(input, { target: { value: '{ "age": "thirty" }' } });
    await userEvent.click(screen.getByRole('button', { name: /try checkout/i }));
    await screen.findByText('$.age: expected integer, got string');

    fireEvent.change(input, { target: { value: '{ "age": 30, "isActive": true, "orderCount": 3 }' } });
    await userEvent.click(screen.getByRole('button', { name: /try checkout/i }));

    expect(await screen.findByText('Approved')).toBeDefined();
    expect(fetchSpy).toHaveBeenCalledWith('/api/checkout', expect.objectContaining({ method: 'POST' }));
    expect(screen.queryByText('$.age: expected integer, got string')).toBeNull();
  });

  it('does not enforce schemas when the catalog carries no model type map', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(JSON.stringify(approval), { status: 200 }));
    render(<CheckoutPane client={clientWith({ specs: [], collections: [] })} />);
    await settleCatalog();

    fireEvent.change(screen.getByLabelText(/customer/i), {
      target: { value: '{ "age": "thirty" }' },
    });
    await userEvent.click(screen.getByRole('button', { name: /try checkout/i }));

    expect(await screen.findByText('Approved')).toBeDefined();
    expect(fetchSpy).toHaveBeenCalled();
  });
});
