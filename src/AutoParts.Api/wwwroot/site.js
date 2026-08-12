const form = document.querySelector('#search-form');
const result = document.querySelector('#result');
const status = document.querySelector('#status');
const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

function showText(selector, text) { document.querySelector(selector).textContent = text || '—'; }
function price(value) { return value == null ? '[Preencher]' : money.format(value); }

async function loadListing(oem) {
  status.textContent = 'Gerando amostra…';
  result.hidden = true;
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}`);
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'Não foi possível localizar a peça.');
    showText('#title', data.title);
    showText('#part-number', data.oemPartNumber);
    showText('#price', price(data.suggestedValue));
    showText('#keywords', data.keywordGroup);
    showText('#applications', data.applications);
    showText('#source', data.source);
    document.querySelector('#vehicle-image').src = data.vehicleImageUrl;
    document.querySelector('#realoem').href = data.realoemUrl;
    document.querySelector('#sheet-row').innerHTML = `<tr><td>[Preencher]</td><td>${data.oemPartNumber}</td><td>${data.title}</td><td>${price(data.suggestedValue)}</td><td>${data.keywordGroup}</td><td>${data.applications}</td></tr>`;
    status.textContent = 'Amostra pronta. Verifique no RealOEM antes de publicar.';
    result.hidden = false;
    generateDescription(data.oemPartNumber);
    generateImages(data.oemPartNumber);
  } catch (error) { status.textContent = error.message; }
}

async function generateDescription(oem) {
  const panel = document.querySelector('#description-panel');
  panel.hidden = true;
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}/description`, { method: 'POST' });
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'Não foi possível gerar a descrição.');
    document.querySelector('#listing-description').value = data.description;
    showText('#description-note', `${data.generator}: ${data.verificationNote}`);
    panel.hidden = false;
  } catch (error) { status.textContent = `Anúncio pronto. Descrição pendente: ${error.message}`; }
}

async function generateImages(oem) {
  const section = document.querySelector('#generated-images');
  section.hidden = true;
  status.textContent = 'Gerando as duas imagens do anúncio…';
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}/images`, { method: 'POST' });
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'Não foi possível gerar as imagens.');
    document.querySelector('#primary-image').src = data.primaryImageUrl;
    document.querySelector('#technical-image').src = data.technicalImageUrl;
    section.hidden = false;
    status.textContent = 'Anúncio e duas imagens gerados.';
  } catch (error) { status.textContent = `Anúncio pronto. Imagens pendentes: ${error.message}`; }
}

form.addEventListener('submit', event => { event.preventDefault(); loadListing(new FormData(form).get('oem').trim()); });
document.querySelector('#ai-button').addEventListener('click', async () => {
  const oem = new FormData(form).get('oem').trim();
  if (!oem) return;
  status.textContent = 'IA verificando dados e redigindo anúncio…';
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}/generate`, { method: 'POST' });
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'Não foi possível gerar a sugestão.');
    showText('#title', data.title);
    showText('#price', price(data.suggestedValue));
    showText('#keywords', data.keywordGroup);
    showText('#applications', data.applications);
    showText('#verification', `${data.compatibilityStatus}: ${data.verificationNote}`);
    document.querySelector('#sheet-row').innerHTML = `<tr><td>[Preencher]</td><td>${oem}</td><td>${data.title}</td><td>${price(data.suggestedValue)}</td><td>${data.keywordGroup}</td><td>${data.applications}</td></tr>`;
    status.textContent = 'Sugestão da IA pronta. Revise a validação antes de publicar.';
  } catch (error) { status.textContent = error.message; }
});
document.querySelector('#copy-description').addEventListener('click', async () => {
  const field = document.querySelector('#listing-description');
  await navigator.clipboard.writeText(field.value);
  status.textContent = 'Descrição copiada.';
});
document.querySelector('#send-prelisting').addEventListener('click', () => {
  const oem = new FormData(form).get('oem').trim();
  document.querySelector('#send-status').textContent = `Pré-anúncio ${oem} preparado para envio ao Drive Parts.`;
});
loadListing(document.querySelector('#oem').value);
