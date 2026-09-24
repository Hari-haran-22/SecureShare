const $ = id => document.getElementById(id);
const id = new URLSearchParams(location.search).get('id');
let session, info;
const status = (text, error = false) => { $('downloadStatus').textContent = text; $('downloadStatus').classList.toggle('error', error); };
async function getError(response) {
    const data = await response.json().catch(() => ({}));
    return data.detail || data.title || 'The request failed. Please try again.';
}
async function init() {
    if (!/^[a-f0-9-]{36}$/i.test(id ?? '')) throw new Error('Invalid share link.');
    const s = await fetch('/api/session');
    if (!s.ok) throw new Error(await getError(s));
    session = await s.json();
    const response = await fetch('/api/files/' + id + '/info');
    if (!response.ok) throw new Error(await getError(response));
    info = await response.json();
    $('fileInfo').textContent = info.originalFilename + ' · ' + (info.sizeBytes / 1048576).toFixed(1) + ' MB · Expires ' + new Date(info.expiresAt).toLocaleString();
    $('passwordLabel').hidden = !info.passwordProtected; $('filePassword').required = info.passwordProtected;
    $('downloadForm').hidden = false;
}
$('downloadForm').addEventListener('submit', async event => {
    event.preventDefault(); $('downloadBtn').disabled = true; status('Validating and downloading…');
    try {
        const response = await fetch('/api/files/' + id + '/download', {method:'POST', headers:{'Content-Type':'application/json','X-CSRF-TOKEN':session.csrfToken}, body:JSON.stringify({password:$('filePassword').value || null})});
        if (!response.ok) throw new Error(await getError(response));
        const blob = await response.blob();
        const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = info.originalFilename; a.click();
        setTimeout(() => URL.revokeObjectURL(url), 60000);
        $('filePassword').value = ''; status('Download received. Check your browser downloads.');
    } catch(e) { status(e.message, true); }
    finally { $('downloadBtn').disabled = false; }
});
init().catch(e => { $('fileInfo').textContent = 'File unavailable'; status(e.message, true); });
