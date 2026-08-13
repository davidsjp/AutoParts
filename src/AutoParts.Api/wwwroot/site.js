const form = document.querySelector('#search-form');
const result = document.querySelector('#result');
const status = document.querySelector('#status');
const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

function showText(selector, text) { document.querySelector(selector).textContent = text || '—'; }
function price(value) { return value == null ? '[Preencher]' : money.format(value); }

async function openRealoemAndCopy(oem) {
  const popup = window.open('https://www.realoem.com/bmw/enUS/select', 'realoem_lookup');
  try { await navigator.clipboard.writeText(oem); } catch { /* Clipboard permission is optional. */ }
  if (popup) popup.focus();
}

async function loadListing(oem) {
  status.textContent = 'Gerando amostra…';
  result.hidden = true;
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}`);
    const data = await response.json();
    if (!response.ok) {
      if (response.status === 404) {
        await openRealoemAndCopy(oem);
        document.querySelector('#realoem-dialog').showModal();
        throw new Error(`OEM ${oem} não está na base. Cole os dados do RealOEM no formulário de importação.`);
      }
      throw new Error(data.detail || data.title || 'Não foi possível localizar a peça.');
    }
    showText('#title', data.title); showText('#part-number', data.oemPartNumber); showText('#price', price(data.suggestedValue));
    showText('#keywords', data.keywordGroup); showText('#applications', data.applications); showText('#source', data.source);
    document.querySelector('#realoem').href = data.realoemUrl;
    document.querySelector('#sheet-row').innerHTML = `<tr><td>[Preencher]</td><td>${data.oemPartNumber}</td><td>${data.title}</td><td>${price(data.suggestedValue)}</td><td>${data.keywordGroup}</td><td>${data.applications}</td></tr>`;
    status.textContent = 'Amostra pronta. Verifique no RealOEM antes de publicar.';
    result.hidden = false;
    generateDescription(data.oemPartNumber);
    generateImages(data.oemPartNumber, false);
  } catch (error) { status.textContent = error.message; }
}

async function generateDescription(oem) {
  const panel = document.querySelector('#description-panel'); panel.hidden = true;
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}/description`, { method: 'POST' });
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'Não foi possível gerar a descrição.');
    document.querySelector('#listing-description').value = data.description;
    showText('#description-note', `${data.generator}: ${data.verificationNote}`); panel.hidden = false;
  } catch (error) { status.textContent = `Anúncio pronto. Descrição pendente: ${error.message}`; }
}

async function generateImages(oem, regenerate = false) {
  const section = document.querySelector('#generated-images'); section.hidden = true;
  status.textContent = regenerate ? 'Gerando novas imagens pelo OpenAI…' : 'Gerando as imagens do anúncio…';
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}/images?regenerate=${regenerate}`, { method: 'POST' });
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'Não foi possível gerar as imagens.');
    document.querySelector('#primary-image').src = `${data.primaryImageUrl}?v=${Date.now()}`;
    document.querySelector('#technical-image').src = `${data.technicalImageUrl}?v=${Date.now()}`;
    section.hidden = false; status.textContent = regenerate ? 'Novas imagens foram geradas pelo OpenAI.' : 'Anúncio e imagens gerados.';
  } catch (error) { status.textContent = `Anúncio pronto. Imagens pendentes: ${error.message}`; }
}

form.addEventListener('submit', event => { event.preventDefault(); loadListing(new FormData(form).get('oem').trim()); });
document.querySelector('#ai-button').addEventListener('click', async () => {
  const oem = new FormData(form).get('oem').trim(); if (!oem) return;
  status.textContent = 'IA verificando dados e redigindo anúncio…';
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}/generate`, { method: 'POST' }); const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'Não foi possível gerar a sugestão.');
    showText('#title', data.title); showText('#price', price(data.suggestedValue)); showText('#keywords', data.keywordGroup); showText('#applications', data.applications);
    showText('#verification', `${data.compatibilityStatus}: ${data.verificationNote}`);
    document.querySelector('#sheet-row').innerHTML = `<tr><td>[Preencher]</td><td>${oem}</td><td>${data.title}</td><td>${price(data.suggestedValue)}</td><td>${data.keywordGroup}</td><td>${data.applications}</td></tr>`;
    await generateImages(oem, true); await generateDescription(oem);
  } catch (error) { status.textContent = error.message; }
});
document.querySelector('#realoem-button').addEventListener('click', async () => {
  const oem = new FormData(form).get('oem').trim();
  if (!oem) { status.textContent = 'Digite um Part Number antes de abrir o RealOEM.'; return; }
  await openRealoemAndCopy(oem); document.querySelector('#realoem-dialog').showModal();
  status.textContent = `RealOEM aberto. OEM ${oem} copiado para a área de transferência.`;
});
document.querySelector('#close-realoem').addEventListener('click', () => document.querySelector('#realoem-dialog').close());
document.querySelector('#realoem-import-form').addEventListener('submit', async event => {
  event.preventDefault(); const oem = new FormData(form).get('oem').trim();
  const payload = { oemPartNumber: oem, copiedData: document.querySelector('#realoem-data').value, description: document.querySelector('#realoem-description').value, applications: document.querySelector('#realoem-applications').value, keywordGroup: null };
  status.textContent = 'Importando e tratando dados do RealOEM…';
  try {
    const response = await fetch('/api/realoem/import', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }); const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'Não foi possível importar os dados.');
    document.querySelector('#realoem-dialog').close(); await loadListing(data.oemPartNumber);
    document.querySelector('#ai-button').click();
  } catch (error) { status.textContent = error.message; }
});
document.querySelector('#copy-description').addEventListener('click', async () => { await navigator.clipboard.writeText(document.querySelector('#listing-description').value); status.textContent = 'Descrição copiada.'; });
document.querySelector('#send-prelisting').addEventListener('click', () => { const oem = new FormData(form).get('oem').trim(); document.querySelector('#send-status').textContent = `Pré-anúncio ${oem} preparado para envio ao Drive Parts.`; });
loadListing(document.querySelector('#oem').value);
