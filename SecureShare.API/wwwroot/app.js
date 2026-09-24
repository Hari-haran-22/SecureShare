let session;
let xhr;
const $ = id => document.getElementById(id);
const message = (text, error = false) => { $('status').textContent = text; $('status').classList.toggle('error', error); };
const absolute = path => new URL(path, location.origin).href;
const size = bytes => bytes < 1048576 ? (bytes / 1024).toFixed(1) + ' KB' : (bytes / 1048576).toFixed(1) + ' MB';
async function readError(response) {
    const data = await response.json().catch(() => ({}));
    return data.detail || data.title || (response.status === 401 ? 'Your session expired. Reload this page.' : 'The request failed. Please try again.');
}
async function request(url, options = {}) {
    options.headers = { ...options.headers, 'X-CSRF-TOKEN': session.csrfToken };
    const response = await fetch(url, options);
    if (!response.ok) throw new Error(await readError(response));
    return response.status === 204 || response.headers.get('content-length') === '0' ? null : response.json().catch(() => null);
}
async function init() {
    const response = await fetch('/api/session');
    if (!response.ok) throw new Error(await readError(response));
    session = await response.json();
    $('recoveryCode').textContent = session.recoveryCode;
    $('fileHint').textContent = 'Up to ' + size(session.maxFileBytes);
    $('expiryHours').max = session.maxExpiryHours;
    $('maxDownloads').max = session.maxDownloads;
    await refresh();
}
async function copy(text, label) {
    try { await navigator.clipboard.writeText(text); message(label + ' copied.'); }
    catch { message('Copy this text manually: ' + text); }
}
async function refresh() {
    const items = await request('/api/files');
    $('fileList').replaceChildren();
    if (!items.length) { $('fileList').textContent = 'No uploads yet. Your shared files will appear here.'; return; }
    for (const item of items) {
        const f = item.file;
        const row = document.createElement('article'); row.className = 'file-row';
        const info = document.createElement('div');
        const title = document.createElement('div'); title.className = 'file-title'; title.textContent = f.originalFilename;
        const meta = document.createElement('p'); meta.className = 'file-meta';
        meta.textContent = size(f.sizeBytes) + ' · ' + f.downloadCount + '/' + (f.maxDownloads ?? '∞') + ' downloads · ' + (f.isActive ? 'Active' : 'Unavailable') + (f.passwordProtected ? ' · Password protected' : '');
        const expiry = document.createElement('p'); expiry.className = 'file-meta'; expiry.textContent = 'Expires ' + new Date(f.expiresAt).toLocaleString();
        info.append(title, meta, expiry);
        const actions = document.createElement('div'); actions.className = 'actions';
        function action(label, fn, danger = false) {
            const button = document.createElement('button'); button.type = 'button'; button.textContent = label; button.className = danger ? 'danger' : 'secondary';
            button.addEventListener('click', async () => { button.disabled = true; try { await fn(); } catch(e) { message(e.message, true); } finally { button.disabled = false; } });
            actions.append(button);
        }
        if (f.isActive) {
            action('Copy link', () => copy(absolute(item.url), 'Link'));
            action('Revoke', async () => { await request('/api/files/' + f.id + '/revoke', {method:'POST'}); message('Link revoked.'); await refresh(); });
        }
        action('History', async () => {
            const logs = await request('/api/files/' + f.id + '/logs');
            $('logsContent').replaceChildren();
            if (!logs.length) $('logsContent').textContent = 'No download transfers have been authorized.';
            for (const log of logs) {
                const p = document.createElement('p'); p.className = 'log'; p.textContent = new Date(log.accessedAt).toLocaleString() + ' · ' + log.ipAddress + ' · ' + log.userAgent; $('logsContent').append(p);
            }
            $('logsDialog').showModal();
        });
        action('Delete', async () => { await request('/api/files/' + f.id, {method:'DELETE'}); message('File and its history deleted.'); await refresh(); }, true);
        row.append(info, actions); $('fileList').append(row);
    }
}
$('uploadForm').addEventListener('submit', event => {
    event.preventDefault();
    const file = $('fileInput').files[0];
    if (!session || !file) return;
    if (file.size === 0 || file.size > session.maxFileBytes) { message('Choose a non-empty file up to ' + size(session.maxFileBytes) + '.', true); return; }
    const form = new FormData(); form.append('file', file); form.append('expiryHours', $('expiryHours').value); form.append('maxDownloads', $('maxDownloads').value);
    if ($('password').value) form.append('password', $('password').value);
    $('uploadBtn').disabled = true; $('uploadProgress').hidden = false; $('resultArea').hidden = true; $('progress').value = 0; message('');
    xhr = new XMLHttpRequest();
    xhr.open('POST', '/api/files/upload'); xhr.setRequestHeader('X-CSRF-TOKEN', session.csrfToken);
    xhr.upload.onprogress = e => {
        if (e.lengthComputable) { $('progress').value = Math.round(e.loaded / e.total * 100); $('progressText').textContent = $('progress').value === 100 ? 'Scanning and encrypting…' : 'Uploading ' + $('progress').value + '%'; }
    };
    xhr.onload = async () => {
        let data = {};
        try { data = JSON.parse(xhr.responseText || '{}'); } catch { /* A proxy may return a non-JSON error page. */ }
        if (xhr.status >= 200 && xhr.status < 300) {
            $('downloadUrl').textContent = absolute(data.url); $('downloadUrl').href = absolute(data.url);
            $('expiryResult').textContent = 'Expires ' + new Date(data.expiresAt).toLocaleString() + ' · ' + $('maxDownloads').value + ' allowed downloads';
            $('resultArea').hidden = false; $('password').value = ''; $('fileInput').value = ''; message('File scanned, encrypted, and ready to share.');
            try { await refresh(); } catch(e) { message(e.message, true); }
        } else message(data.detail || data.title || 'Upload failed. Please try again.', true);
    };
    xhr.onerror = () => message('Network error. Check your connection and refresh your uploads.', true);
    xhr.onabort = () => { message('Upload canceled. Refresh your dashboard to check whether it completed.'); refresh().catch(() => {}); };
    xhr.onloadend = () => { $('uploadBtn').disabled = false; $('uploadProgress').hidden = true; };
    xhr.send(form);
});
$('cancelBtn').onclick = () => xhr?.abort();
$('copyBtn').onclick = () => copy($('downloadUrl').href, 'Link');
$('saveRecovery').onclick = () => copy(session.recoveryCode, 'Recovery code');
$('refreshBtn').onclick = () => refresh().catch(e => message(e.message, true));
$('closeLogs').onclick = () => $('logsDialog').close();
$('restoreForm').addEventListener('submit', async event => {
    event.preventDefault();
    try {
        await request('/api/session/restore', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({code:$('restoreCode').value.trim()})});
        $('restoreCode').value = ''; await init(); message('Dashboard access restored.');
    } catch(e) { message(e.message, true); }
});
const drop = document.querySelector('.drop');
drop.addEventListener('dragover', e => { e.preventDefault(); drop.classList.add('dragging'); });
drop.addEventListener('dragleave', () => drop.classList.remove('dragging'));
drop.addEventListener('drop', e => { e.preventDefault(); drop.classList.remove('dragging'); if (e.dataTransfer.files.length) $('fileInput').files = e.dataTransfer.files; });
init().catch(e => message(e.message, true));
