const vehicleSearch = document.querySelector('#vehicle-search');
const vehicleCards = document.querySelector('#vehicle-cards');
const vehicleStatus = document.querySelector('#vehicle-status');
const vehicleParts = document.querySelector('#vehicle-parts');
const vehiclePartsTitle = document.querySelector('#vehicle-parts-title');
const vehiclePartsStatus = document.querySelector('#vehicle-parts-status');
const vehiclePartsRows = document.querySelector('#vehicle-parts-rows');
const vehiclePartSearch = document.querySelector('#vehicle-part-search');
const vehiclePagePrevious = document.querySelector('#vehicle-page-prev');
const vehiclePageNext = document.querySelector('#vehicle-page-next');
const vehiclePageInfo = document.querySelector('#vehicle-page-info');
let vehicleSearchTimer;
let vehiclePartSearchTimer;
let currentVehicles = new Map();
let currentVehicleId = null;
let currentVehiclePage = 0;
const vehiclePageSize = 50;

const escapeHtml = value => String(value ?? '').replace(/[&<>'"]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[character]);
const vehicleName = vehicle => [vehicle.manufacturer, vehicle.model].filter(Boolean).join(' ');
const partSidePosition = part => [part.side, part.position].filter(Boolean).join(' / ') || '-';
const yearFromDate = value => value ? String(value).slice(0, 4) : '';
const partCompatibility = part => {
  const vehicle = [part.manufacturer, part.model, part.chassis, part.engine, part.typeCode].filter(Boolean).join(' ');
  const start = yearFromDate(part.productionStart);
  const end = yearFromDate(part.productionEnd);
  const years = start && end ? `${start} a ${end}` : start ? `desde ${start}` : '';
  return [vehicle, years].filter(Boolean).join(' | ') || '-';
};
const vehicleDetail = vehicle => [vehicle.chassis, vehicle.engine, vehicle.typeCode].filter(Boolean).join(' · ') || 'Modelo BMW';
const fallbackVehicleImage = '/images/vehicle-card-fallback-v1.png';

function renderVehicleCards(vehicles) {
  currentVehicles = new Map(vehicles.map(vehicle => [vehicle.id, vehicle]));
  if (!vehicles.length) {
    vehicleCards.innerHTML = '<p>Nenhum veiculo encontrado no banco para esta busca.</p>';
    return;
  }
  vehicleCards.innerHTML = vehicles.map(vehicle => {
    const image = escapeHtml(vehicle.imageUrl || fallbackVehicleImage);
    return `<button type="button" class="vehicle-card" data-vehicle-id="${vehicle.id}" aria-label="Ver pecas de ${escapeHtml(vehicleName(vehicle))}">
      <img src="${image}" alt="${escapeHtml(vehicleName(vehicle))}" onerror="this.onerror=null;this.src='${fallbackVehicleImage}'">
      <span class="vehicle-card-body"><h3>${escapeHtml(vehicleName(vehicle))}</h3><p>${escapeHtml(vehicleDetail(vehicle))}</p><small>${vehicle.partCount.toLocaleString('pt-BR')} pecas no banco</small></span>
    </button>`;
  }).join('');
}

async function loadVehicleCards() {
  vehicleStatus.textContent = 'Carregando veiculos disponiveis...';
  try {
    const search = vehicleSearch.value.trim();
    const response = await fetch(`/api/vehicle-catalog?take=24&search=${encodeURIComponent(search)}`);
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || 'Nao foi possivel carregar os veiculos.');
    renderVehicleCards(data);
    vehicleStatus.textContent = `${data.length} modelo${data.length === 1 ? '' : 's'} exibido${data.length === 1 ? '' : 's'}.`;
  } catch (error) {
    vehicleCards.innerHTML = '';
    vehicleStatus.textContent = error.message;
  }
}

async function loadVehicleParts(vehicleId, requestedPage = 0) {
  const vehicle = currentVehicles.get(vehicleId);
  if (!vehicle) return;
  currentVehicleId = vehicleId;
  currentVehiclePage = Math.max(requestedPage, 0);
  vehicleParts.hidden = false;
  vehiclePartsTitle.textContent = vehicleName(vehicle);
  vehiclePartsStatus.textContent = 'Carregando pecas cadastradas para este modelo...';
  vehiclePartsRows.innerHTML = '';
  try {
    const skip = currentVehiclePage * vehiclePageSize;
    const search = vehiclePartSearch.value.trim();
    const response = await fetch(`/api/vehicle-catalog/${vehicleId}/parts?take=${vehiclePageSize}&skip=${skip}&search=${encodeURIComponent(search)}`);
    const page = await response.json();
    if (!response.ok) throw new Error(page.detail || 'Nao foi possivel carregar as pecas.');
    const pageCount = Math.max(1, Math.ceil(page.total / vehiclePageSize));
    if (currentVehiclePage >= pageCount && page.total > 0) return loadVehicleParts(vehicleId, pageCount - 1);
    vehiclePartsStatus.textContent = `${page.total.toLocaleString('pt-BR')} pecas comerciais disponiveis para este veiculo.`;
    vehiclePartsRows.innerHTML = page.items.map(part => `<tr><td>${escapeHtml(part.oemPartNumber)}</td><td>${escapeHtml(part.description)}</td><td>${escapeHtml(part.category)}</td><td>${escapeHtml(partSidePosition(part))}</td><td>${escapeHtml(partCompatibility(part))}</td><td class="${part.source === 'RealOEM.com' ? 'source-confirmed' : 'source-review'}">${escapeHtml(part.source)}</td><td><button type="button" class="vehicle-part-action" data-oem="${escapeHtml(part.oemPartNumber)}">Gerar anuncio</button></td></tr>`).join('');
    vehiclePageInfo.textContent = `Pagina ${currentVehiclePage + 1} de ${pageCount}`;
    vehiclePagePrevious.disabled = currentVehiclePage === 0;
    vehiclePageNext.disabled = currentVehiclePage >= pageCount - 1;
    vehicleParts.scrollIntoView({ behavior: 'smooth', block: 'start' });
  } catch (error) {
    vehiclePartsStatus.textContent = error.message;
  }
}

vehicleSearch.addEventListener('input', () => {
  clearTimeout(vehicleSearchTimer);
  vehicleSearchTimer = setTimeout(loadVehicleCards, 250);
});
vehicleCards.addEventListener('click', event => {
  const card = event.target.closest('[data-vehicle-id]');
  if (card) loadVehicleParts(Number(card.dataset.vehicleId));
});
vehiclePartsRows.addEventListener('click', event => {
  const button = event.target.closest('[data-oem]');
  if (!button) return;
  document.querySelector('#oem').value = button.dataset.oem;
  document.querySelector('#catalogo').scrollIntoView({ behavior: 'smooth', block: 'start' });
  loadListing(button.dataset.oem);
});
vehiclePartSearch.addEventListener('input', () => {
  clearTimeout(vehiclePartSearchTimer);
  vehiclePartSearchTimer = setTimeout(() => { if (currentVehicleId) loadVehicleParts(currentVehicleId, 0); }, 250);
});
vehiclePagePrevious.addEventListener('click', () => { if (currentVehicleId && currentVehiclePage > 0) loadVehicleParts(currentVehicleId, currentVehiclePage - 1); });
vehiclePageNext.addEventListener('click', () => { if (currentVehicleId) loadVehicleParts(currentVehicleId, currentVehiclePage + 1); });
document.querySelector('#close-vehicle-parts').addEventListener('click', () => vehicleParts.hidden = true);
document.querySelector('a[href="#veiculos"]')?.addEventListener('click', event => {
  event.preventDefault();
  document.querySelector('#veiculos').scrollIntoView({ behavior: 'smooth', block: 'start' });
  vehicleSearch.focus({ preventScroll: true });
  document.querySelector('#veiculos').classList.remove('vehicle-browser-highlight');
  requestAnimationFrame(() => document.querySelector('#veiculos').classList.add('vehicle-browser-highlight'));
});
loadVehicleCards();

const form = document.querySelector('#search-form');
const result = document.querySelector('#result');
const status = document.querySelector('#status');
const lookupOptions = document.querySelector('#lookup-options');
const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const showText = (selector, text) => document.querySelector(selector).textContent = text || '—';
const price = value => value == null ? '[Preencher]' : money.format(value);
const oemValue = () => new FormData(form).get('oem').trim();

function searchFromHeader() {
  const value = document.querySelector('#header-search').value.trim();
  if (!value) return;
  document.querySelector('#oem').value = value;
  loadListing(value);
}

async function openRealoemAndCopy(oem) {
  const popup = window.open('https://www.realoem.com/bmw/enUS/select', 'realoem_lookup');
  try { await navigator.clipboard.writeText(oem); } catch { /* Permission is optional. */ }
  if (popup) popup.focus();
}

function renderListing(data) {
  showText('#title', data.title); showText('#part-number', data.oemPartNumber); showText('#price', price(data.suggestedValue));
  showText('#keywords', data.keywordGroup); showText('#applications', data.applications); showText('#source', data.source);
  document.querySelector('#source').className = data.source === 'RealOEM.com' ? 'source-confirmed' : 'source-review';
  document.querySelector('#realoem').href = data.realoemUrl;
  document.querySelector('#sheet-row').innerHTML = `<tr><td>[Preencher]</td><td>${data.oemPartNumber}</td><td>${data.title}</td><td>${price(data.suggestedValue)}</td><td>${data.keywordGroup}</td><td>${data.applications}</td></tr>`;
}

async function loadListing(oem) {
  status.textContent = 'Buscando peça no banco…'; result.hidden = true; lookupOptions.hidden = true;
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}`); const data = await response.json();
    if (!response.ok) {
      if (response.status === 404) { lookupOptions.hidden = false; throw new Error(`OEM ${oem} não está no banco. Escolha BMV.parts ou RealOEM abaixo.`); }
      throw new Error(data.detail || data.title || 'Não foi possível localizar a peça.');
    }
    renderListing(data); result.hidden = false;
    status.textContent = data.source === 'RealOEM.com' ? 'Dados confirmados pelo RealOEM.' : 'Dados encontrados. Fonte em vermelho: revise antes de publicar.';
    generateDescription(data.oemPartNumber); generateImages(data.oemPartNumber);
  } catch (error) { status.textContent = error.message; }
}

async function generateDescription(oem) {
  const panel = document.querySelector('#description-panel'); panel.hidden = true;
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}/description`, { method: 'POST' }); const data = await response.json();
    if (!response.ok) return;
    document.querySelector('#listing-description').value = data.description;
    showText('#description-note', `${data.generator}: ${data.verificationNote}`); panel.hidden = false;
  } catch { /* Description panel is optional. */ }
}

async function generateImages(oem) {
  const section = document.querySelector('#generated-images'); section.hidden = true;
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}/images`, { method: 'POST' }); const data = await response.json();
    if (!response.ok) return;
    document.querySelector('#primary-image').src = `${data.primaryImageUrl}?v=${Date.now()}`;
    document.querySelector('#technical-image').src = `${data.technicalImageUrl}?v=${Date.now()}`; section.hidden = false;
  } catch { /* API images are optional until API billing is active. */ }
}

form.addEventListener('submit', event => { event.preventDefault(); loadListing(oemValue()); });
document.querySelector('#header-search-button').addEventListener('click', searchFromHeader);
document.querySelector('#header-search').addEventListener('keydown', event => { if (event.key === 'Enter') { event.preventDefault(); searchFromHeader(); } });
document.querySelector('#bmv-import-button').addEventListener('click', async () => {
  const oem = oemValue(); if (!oem) return;
  status.textContent = 'Consultando BMV.parts e importando a peça…';
  try {
    const response = await fetch(`/api/external-catalog/import/${encodeURIComponent(oem)}`, { method: 'POST' }); const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'Peça não encontrada no BMV.parts.');
    await loadListing(data.oemPartNumber);
  } catch (error) { status.textContent = `${error.message} Use o RealOEM para consultar e importar.`; lookupOptions.hidden = false; }
});
document.querySelector('#ai-button').addEventListener('click', async () => {
  const oem = oemValue(); if (!oem) return;
  status.textContent = 'IA revisando anúncio…';
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}/generate`, { method: 'POST' }); const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'Não foi possível revisar o anúncio.');
    showText('#title', data.title); showText('#price', price(data.suggestedValue)); showText('#keywords', data.keywordGroup); showText('#applications', data.applications);
    showText('#verification', `${data.compatibilityStatus}: ${data.verificationNote}`);
    document.querySelector('#sheet-row').innerHTML = `<tr><td>[Preencher]</td><td>${oem}</td><td>${data.title}</td><td>${price(data.suggestedValue)}</td><td>${data.keywordGroup}</td><td>${data.applications}</td></tr>`;
    generateDescription(oem); generateImages(oem); status.textContent = 'Sugestão atualizada. Confirme as aplicações antes de publicar.';
  } catch (error) { status.textContent = error.message; }
});
document.querySelector('#realoem-button').addEventListener('click', async () => {
  const oem = oemValue(); if (!oem) { status.textContent = 'Digite um Part Number antes de abrir o RealOEM.'; return; }
  await openRealoemAndCopy(oem); document.querySelector('#realoem-dialog').showModal();
  status.textContent = `RealOEM aberto e OEM ${oem} copiado. Cole os dados selecionados no painel.`;
});
document.querySelector('#realoem-missing-button').addEventListener('click', () => document.querySelector('#realoem-button').click());
document.querySelector('#close-realoem').addEventListener('click', () => document.querySelector('#realoem-dialog').close());
document.querySelector('#realoem-import-form').addEventListener('submit', async event => {
  event.preventDefault();
  const payload = { oemPartNumber: oemValue(), copiedData: document.querySelector('#realoem-data').value, description: document.querySelector('#realoem-description').value, applications: document.querySelector('#realoem-applications').value, keywordGroup: null };
  status.textContent = 'Importando dados do RealOEM…';
  try {
    const response = await fetch('/api/realoem/import', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }); const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'Não foi possível importar os dados.');
    document.querySelector('#realoem-dialog').close(); await loadListing(data.oemPartNumber);
  } catch (error) { status.textContent = error.message; }
});
document.querySelector('#copy-description').addEventListener('click', async () => { await navigator.clipboard.writeText(document.querySelector('#listing-description').value); status.textContent = 'Descrição copiada.'; });
document.querySelector('#send-prelisting').addEventListener('click', () => document.querySelector('#send-status').textContent = `Pré-anúncio ${oemValue()} preparado para envio ao Drive Parts.`);
loadListing(document.querySelector('#oem').value);
