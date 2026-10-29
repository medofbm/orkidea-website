const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('Orkideya/Areas/Admin/Views/Orders/Index.cshtml', 'utf8');
const script = source.match(/<script>([\s\S]*?)<\/script>/)[1]
    .replace(/@Url\.Action\("([^"]+)"\)/g, '/Admin/Orders/$1');
function setup({ confirmed = true, ok = true, message = 'Saved' } = {}) {
    const requests = [], toasts = [], reloads = [];
    const context = vm.createContext({
        AdminUtils: { confirm: async () => confirmed, showToast: (...args) => toasts.push(args) },
        document: { querySelector: () => ({ value: 'synthetic-csrf' }) },
        window: { location: { reload: () => reloads.push(true) } },
        fetch: async (...args) => { requests.push(args); return { ok, json: async () => ({ success: ok, message }) }; },
        setTimeout: callback => callback(), console: { error() {} }
    });
    vm.runInContext(script, context);
    return { context, requests, toasts, reloads };
}
test('status save sends rendered revision and CSRF and reloads saved counts', async () => {
    const state = setup();
    await state.context.updateOrderStatus(7, 'processing', 3);
    assert.equal(state.requests[0][0], '/Admin/Orders/UpdateStatus?id=7&status=processing&expectedRevision=3');
    assert.equal(state.requests[0][1].method, 'POST');
    assert.equal(state.requests[0][1].headers.RequestVerificationToken, 'synthetic-csrf');
    assert.equal(state.requests[0][1].credentials, 'same-origin');
    assert.equal(state.reloads.length, 1);
    assert.equal(state.toasts[0][1], 'success');
});
test('stale edit displays the server conflict and does not claim success or remove a row', async () => {
    const state = setup({ ok: false, message: 'Refresh this stale page' });
    await state.context.updateOrderStatus(7, 'processing', 3);
    assert.deepEqual(state.toasts[0], ['Refresh this stale page', 'error']);
    assert.equal(state.reloads.length, 0);
});
test('declining confirmation never submits a status update', async () => {
    const state = setup({ confirmed: false });
    await state.context.updateOrderStatus(7, 'processing', 3);
    assert.equal(state.requests.length, 0);
    assert.equal(state.reloads.length, 0);
});
