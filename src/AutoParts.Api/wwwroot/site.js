const $ = selector => document.querySelector(selector);
const escapeHtml = value => String(value ?? '').replace(/[&<>'"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[char]);
const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const year = value => value ? String(value).slice(0, 4) : '';
const vehicleName = vehicle => [vehicle.manufacturer, vehicle.model].filter(Boolean).join(' ');
const vehicleDetail = vehicle => [vehicle.chassis, vehicle.engine, vehicle.typeCode].filter(Boolean).join(' | ') || 'Modelo BMW';
const fallbackVehicleImage = '/images/vehicle-card-fallback-v1.png';
let currentListing;
let currentCompatibilityPartId;
let currentCompatibilities = [];
let activeCompatibilityModel;
let compatibilitySaved = false;
let currentVehicles = new Map();
let currentVehicleId;
let currentVehiclePage = 0;
let vehicleSearchTimer;
let vehiclePartSearchTimer;
let categoriesLoaded = false;
const vehiclePageSize = 50;

function activateView(viewId) {
  document.querySelectorAll('.workspace-view').forEach(view => { view.hidden = view.id !== viewId; });
  document.querySelectorAll('.header-tab').forEach(tab => tab.classList.toggle('is-active', tab.dataset.viewTarget === viewId));
  if (viewId === 'veiculos') loadVehicleCards();
  if (viewId === 'categorias') loadCategories();
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

document.querySelectorAll('.header-tab').forEach(tab => tab.addEventListener('click', () => activateView(tab.dataset.viewTarget)));

function setWorkflowStage(stage) {
  const stages = ['part', 'compatibility', 'listing'];
  const activeIndex = stages.indexOf(stage);
  document.querySelectorAll('[data-workflow-stage]').forEach(element => {
    const index = stages.indexOf(element.dataset.workflowStage);
    element.classList.toggle('is-active', index === activeIndex);
    element.classList.toggle('is-complete', index < activeIndex);
  });
}

function renderListing(data) {
  $('#title').textContent = data.title || '-';
  $('#part-number').textContent = data.oemPartNumber || '-';
  $('#price').textContent = data.suggestedValue == null ? '[Preencher]' : money.format(data.suggestedValue);
  renderPriceRange(data.priceRange);
  $('#keywords').textContent = data.keywordGroup || '-';
  renderApplications(data.applications);
  $('#source').textContent = data.source || '-';
  $('#source').className = data.source === 'RealOEM.com' ? 'source-confirmed' : 'source-review';
  $('#realoem').href = data.realoemUrl || 'https://www.realoem.com/bmw/enUS/select';
  $('#sheet-row').innerHTML = `<tr><td>${escapeHtml(data.oemPartNumber)}</td><td>${escapeHtml(data.title)}</td><td>${data.suggestedValue == null ? '[Preencher]' : escapeHtml(money.format(data.suggestedValue))}</td><td>${escapeHtml(data.applications)}</td></tr>`;
  setWorkflowStage('part');
}

function renderApplications(value) {
  const items = String(value || '').split(';').map(item => item.trim()).filter(Boolean);
  $('#applications').innerHTML = items.length
    ? items.map(item => `<span class="application-tag">${escapeHtml(item)}</span>`).join('')
    : '<span class="application-tag">Nenhuma aplicacao confirmada.</span>';
}

function renderPriceRange(range) {
  if (!range) { $('#price-range').textContent = 'Sem anuncios cadastrados.'; return; }
  const label = `${money.format(range.minimum)} a ${money.format(range.maximum)}`;
  $('#price-range').textContent = range.isSample ? `${label} (exemplo ficticio)` : `${label} (${range.listingCount} anuncio(s))`;
}

async function loadCompatibilityEditor(oem) {
  const lookupResponse = await fetch(`/api/parts/oem/${encodeURIComponent(oem)}/compatibility`);
  const lookup = await lookupResponse.json();
  if (!lookupResponse.ok) throw new Error(lookup.detail || 'Nao foi possivel consultar as compatibilidades.');
  currentCompatibilityPartId = lookup.partId;
  const response = await fetch(`/api/parts/${lookup.partId}/compatibilities`);
  const compatibilities = await response.json();
  if (!response.ok) throw new Error(compatibilities.detail || 'Nao foi possivel carregar os veiculos compativeis.');
  currentCompatibilities = compatibilities.map(item => ({ ...item, selected: item.status === 'Confirmed' }));
  activeCompatibilityModel = undefined;
  compatibilitySaved = false;
  $('#compatibility-editor').hidden = currentCompatibilities.length === 0;
  if (currentCompatibilities.length) {
    renderCompatibilityEditor();
    setWorkflowStage('compatibility');
  }
}

function compatibilityLabel(item) {
  const vehicle = item.vehicle;
  const years = [year(item.productionStart), year(item.productionEnd)].filter(Boolean).join(' a ');
  return [vehicle.manufacturer, vehicle.model, vehicle.chassis, vehicle.engine, years].filter(Boolean).join(' | ');
}

function visibleCompatibilities() {
  return currentCompatibilities.filter(item => item.selected || item.relevance > 0);
}

function renderCompatibilityEditor() {
  const models = new Map();
  visibleCompatibilities().forEach(item => {
    const key = [item.vehicle.manufacturer, item.vehicle.model].filter(Boolean).join(' | ');
    if (!models.has(key)) models.set(key, []);
    models.get(key).push(item);
  });
  const modelGroups = [...models.entries()].sort(([firstLabel, firstItems], [secondLabel, secondItems]) =>
    Math.max(...secondItems.map(item => item.relevance)) - Math.max(...firstItems.map(item => item.relevance)) || firstLabel.localeCompare(secondLabel));
  if ((!activeCompatibilityModel || !models.has(activeCompatibilityModel)) && modelGroups.length) activeCompatibilityModel = modelGroups[0][0];
  $('#compatibility-models').innerHTML = `<div class="model-tabs">${modelGroups.map(([label, items]) => {
    const selected = items.some(item => item.selected);
    const allSelected = items.every(item => item.selected);
    const maxRelevance = Math.max(...items.map(item => item.relevance));
    return `<article class="model-selection-card"><button type="button" class="model-tab ${label === activeCompatibilityModel ? 'is-active' : ''}" data-compatibility-model="${escapeHtml(label)}"><strong>${escapeHtml(label)}</strong></button><div class="model-card-meta"><span>${items.length} vers${items.length === 1 ? 'ao' : 'oes'}</span><span class="model-relevance">Relevancia ${maxRelevance}/10</span></div><label><input type="checkbox" data-model-selection="${escapeHtml(label)}" ${allSelected ? 'checked' : ''}> ${allSelected ? 'Incluido no anuncio' : 'Incluir no anuncio'}</label></article>`;
  }).join('')}</div>`;
  const details = models.get(activeCompatibilityModel) || [];
  $('#compatibility-details').innerHTML = `<p class="compatibility-instruction">Selecione as combinacoes de chassi, motor e ano compativeis para <strong>${escapeHtml(activeCompatibilityModel || '')}</strong>.</p><div class="table-wrap"><table><thead><tr><th>USAR</th><th>CHASSI</th><th>MOTOR</th><th>ANO</th><th>PRIORIDADE</th></tr></thead><tbody>${details.map(item => `<tr><td><input type="checkbox" data-compatibility-id="${item.id}" ${item.selected ? 'checked' : ''}></td><td>${escapeHtml(item.vehicle.chassis || '-')}</td><td>${escapeHtml(item.vehicle.engine || '-')}</td><td>${escapeHtml([year(item.productionStart), year(item.productionEnd)].filter(Boolean).join(' a ') || String(item.vehicle.modelYear || '-'))}</td><td>${item.relevance}</td></tr>`).join('')}</tbody></table></div>`;
  $('#compatibility-priority').innerHTML = `<div class="relevance-grid">${[...visibleCompatibilities()].sort((a, b) => b.relevance - a.relevance || compatibilityLabel(a).localeCompare(compatibilityLabel(b))).map(item => `<article class="relevance-card ${item.relevance === 0 && !item.selected ? 'is-inactive' : ''}"><header><div><h3>${escapeHtml(compatibilityLabel(item))}</h3><p>${escapeHtml(item.status)} | relevancia de mercado ${item.vehicle.marketRelevance ?? 0}</p></div><strong>${item.relevance}/10</strong></header><label><input type="checkbox" data-compatibility-selection="${item.id}" ${item.selected ? 'checked' : ''}> Usar no anuncio</label><label class="relevance-control">Relevancia <input type="range" min="0" max="10" value="${item.relevance}" data-compatibility-priority="${item.id}"><output>${item.relevance}</output></label></article>`).join('')}</div>`;
  $('#generate-ad-button').disabled = !compatibilitySaved;
}

function changeSelection(ids, selected) {
  ids.forEach(id => { const item = currentCompatibilities.find(value => value.id === Number(id)); if (item) item.selected = selected; });
  compatibilitySaved = false;
  renderCompatibilityEditor();
}

$('#compatibility-models').addEventListener('click', event => {
  const button = event.target.closest('[data-compatibility-model]');
  if (!button) return;
  activeCompatibilityModel = button.dataset.compatibilityModel;
  renderCompatibilityEditor();
  document.querySelectorAll('.compatibility-tab').forEach(tab => tab.classList.toggle('is-active', tab.dataset.compatibilityTab === 'details'));
  document.querySelectorAll('.compatibility-panel').forEach(panel => { panel.hidden = panel.id !== 'compatibility-details'; });
});
$('#compatibility-models').addEventListener('change', event => {
  const input = event.target.closest('[data-model-selection]');
  if (!input) return;
  const ids = currentCompatibilities
    .filter(item => [item.vehicle.manufacturer, item.vehicle.model].filter(Boolean).join(' | ') === input.dataset.modelSelection)
    .map(item => item.id);
  changeSelection(ids, input.checked);
});
$('#compatibility-details').addEventListener('change', event => {
  const input = event.target.closest('[data-compatibility-id]');
  if (input) changeSelection([input.dataset.compatibilityId], input.checked);
});
$('#compatibility-priority').addEventListener('input', event => {
  const input = event.target.closest('[data-compatibility-priority]');
  if (!input) return;
  const item = currentCompatibilities.find(value => value.id === Number(input.dataset.compatibilityPriority));
  if (!item) return;
  item.relevance = Number(input.value);
  input.nextElementSibling.value = input.value;
  compatibilitySaved = false;
  $('#generate-ad-button').disabled = true;
});
$('#compatibility-priority').addEventListener('change', event => {
  const input = event.target.closest('[data-compatibility-selection]');
  if (input) changeSelection([input.dataset.compatibilitySelection], input.checked);
});
document.querySelectorAll('.compatibility-tab').forEach(tab => tab.addEventListener('click', () => {
  document.querySelectorAll('.compatibility-tab').forEach(item => item.classList.toggle('is-active', item === tab));
  document.querySelectorAll('.compatibility-panel').forEach(panel => { panel.hidden = panel.id !== `compatibility-${tab.dataset.compatibilityTab}`; });
}));

async function saveCompatibilities() {
  $('#compatibility-status').textContent = 'Salvando selecao...';
  try {
    await Promise.all(currentCompatibilities.map(async item => {
      const status = item.selected ? 'Confirmed' : 'Rejected';
      const response = await fetch(`/api/parts/${currentCompatibilityPartId}/compatibilities/${item.id}`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ productionStart: item.productionStart, productionEnd: item.productionEnd, notes: item.notes, relevance: item.relevance, status, confidence: item.confidence, source: item.source, evidenceText: item.evidenceText, confirmedByUserId: item.selected ? 'catalogo-ui' : null })
      });
      const data = await response.json();
      if (!response.ok) throw new Error(data.detail || 'Nao foi possivel salvar a compatibilidade.');
      Object.assign(item, data, { selected: item.selected });
    }));
    renderCompatibilityEditor();
    compatibilitySaved = true;
    $('#generate-ad-button').disabled = false;
    $('#compatibility-status').textContent = 'Selecao salva. Os veiculos marcados foram confirmados e os desmarcados foram rejeitados.';
    return true;
  } catch (error) { $('#compatibility-status').textContent = error.message; return false; }
}

async function generateAd() {
  if (!compatibilitySaved) { $('#compatibility-status').textContent = 'Salve a selecao de anos, motores e chassis antes de gerar o anuncio.'; return; }
  $('#status').textContent = 'Gerando anuncio com as compatibilidades selecionadas...';
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(currentListing.oemPartNumber)}/generate`, { method: 'POST' });
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || 'Nao foi possivel gerar o anuncio.');
    $('#title').textContent = data.title || $('#title').textContent;
    $('#price').textContent = data.suggestedValue == null ? '[Preencher]' : money.format(data.suggestedValue);
    renderPriceRange(currentListing.priceRange);
    $('#keywords').textContent = data.keywordGroup || '-';
    renderApplications(data.applications);
    $('#sheet-row').innerHTML = `<tr><td>${escapeHtml(currentListing.oemPartNumber)}</td><td>${escapeHtml(data.title)}</td><td>${data.suggestedValue == null ? '[Preencher]' : escapeHtml(money.format(data.suggestedValue))}</td><td>${escapeHtml(data.applications)}</td></tr>`;
    $('#ad-title').textContent = data.title || '-';
    $('#ad-models').textContent = currentCompatibilities.filter(item => item.selected).map(compatibilityLabel).join('; ') || '-';
    await loadGeneratedDescription(currentListing.oemPartNumber);
    await loadSavedPhotos(currentListing.oemPartNumber);
    $('#generated-ad-panel').hidden = false;
    selectAdTab('summary');
    setWorkflowStage('listing');
    $('#generated-ad-panel').scrollIntoView({ behavior: 'smooth', block: 'start' });
    $('#status').textContent = 'Anuncio gerado com as compatibilidades selecionadas.';
  } catch (error) { $('#status').textContent = error.message; }
}

function selectAdTab(name) {
  document.querySelectorAll('.ad-tab').forEach(tab => tab.classList.toggle('is-active', tab.dataset.adTab === name));
  document.querySelectorAll('.ad-panel').forEach(panel => { panel.hidden = panel.id !== `ad-${name}`; });
}
async function loadGeneratedDescription(oem) {
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}/description`, { method: 'POST' });
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || 'Descricao indisponivel.');
    $('#listing-description').value = data.description || '';
    $('#description-note').textContent = `${data.generator}: ${data.verificationNote}`;
  } catch (error) { $('#listing-description').value = ''; $('#description-note').textContent = error.message; }
}
async function loadSavedPhotos(oem) {
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}/photos`);
    const photos = await response.json();
    if (!response.ok) throw new Error(photos.detail || 'Fotos indisponiveis.');
    $('#saved-photos').innerHTML = photos.length ? photos.map((photo, index) => `<figure><img src="${escapeHtml(photo)}" alt="Foto salva ${index + 1} do OEM ${escapeHtml(oem)}"><figcaption>${photoCaption(photo, index)}</figcaption></figure>`).join('') : '<p>Nenhuma foto salva para este OEM.</p>';
  } catch (error) { $('#saved-photos').innerHTML = `<p>${escapeHtml(error.message)}</p>`; }
  renderPhotoCompatibilities();
}
function photoCaption(photo, index) {
  if (photo.includes('mercadolivre-')) return 'Versao Mercado Livre';
  if (photo.includes('shopee-')) return 'Versao Shopee';
  if (photo.includes('marketing-')) return 'Imagem marketing';
  if (photo.includes('tecnica-')) return 'Imagem tecnica';
  return index === 0 ? 'Foto principal' : `Foto ${index + 1}`;
}
$('#ad-photos').addEventListener('click', async event => {
  const button = event.target.closest('[data-marketplace-image]');
  if (!button || !currentListing) return;
  const channel = button.dataset.marketplaceImage;
  $('#image-generation-status').textContent = `Gerando imagem para ${channel === 'MercadoLivre' ? 'Mercado Livre' : 'Shopee'}...`;
  button.disabled = true;
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(currentListing.oemPartNumber)}/images/${channel}`, { method: 'POST' });
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || 'Nao foi possivel gerar a imagem.');
    await loadSavedPhotos(currentListing.oemPartNumber);
    $('#image-generation-status').textContent = `Imagem ${channel === 'MercadoLivre' ? 'Mercado Livre' : 'Shopee'} salva nas imagens disponiveis.`;
  } catch (error) { $('#image-generation-status').textContent = error.message; }
  finally { button.disabled = false; }
});
function renderPhotoCompatibilities() {
  const selected = currentCompatibilities.filter(item => item.selected).sort((a, b) => b.relevance - a.relevance);
  $('#photo-compatibilities').innerHTML = selected.length
    ? `<h3>Compatibilidades confirmadas</h3><ul>${selected.map(item => `<li><strong>${escapeHtml(vehicleName(item.vehicle))}</strong><small>Chassi: ${escapeHtml(item.vehicle.chassis || '-')} | Motor: ${escapeHtml(item.vehicle.engine || '-')} | Ano: ${escapeHtml([year(item.productionStart), year(item.productionEnd)].filter(Boolean).join(' a ') || String(item.vehicle.modelYear || '-'))} | Prioridade: ${item.relevance}</small></li>`).join('')}</ul>`
    : '<p>Nenhuma compatibilidade confirmada para este anuncio.</p>';
}
document.querySelectorAll('.ad-tab').forEach(tab => tab.addEventListener('click', () => selectAdTab(tab.dataset.adTab)));

$('#save-compatibilities').addEventListener('click', saveCompatibilities);
$('#generate-ad-button').addEventListener('click', generateAd);

async function loadListing(oem) {
  $('#status').textContent = 'Buscando peca no banco...'; $('#result').hidden = true; $('#lookup-options').hidden = true;
  try {
    const response = await fetch(`/api/listings/${encodeURIComponent(oem)}`); const data = await response.json();
    if (!response.ok) {
      if (response.status === 404) $('#lookup-options').hidden = false;
      throw new Error(data.detail || `OEM ${oem} nao esta no banco.`);
    }
    currentListing = data; renderListing(data); $('#result').hidden = false;
    await loadCompatibilityEditor(data.oemPartNumber);
    $('#status').textContent = 'Peca encontrada. Revise os modelos antes de gerar o anuncio.';
  } catch (error) { $('#status').textContent = error.message; }
}

function searchFromHeader() { const value = $('#header-search').value.trim(); if (value) { $('#oem').value = value; activateView('oem-workspace'); loadListing(value); } }
$('#search-form').addEventListener('submit', event => { event.preventDefault(); loadListing($('#oem').value.trim()); });
$('#header-search-button').addEventListener('click', searchFromHeader);
$('#header-search').addEventListener('keydown', event => { if (event.key === 'Enter') { event.preventDefault(); searchFromHeader(); } });
$('#bmv-import-button').addEventListener('click', async () => {
  const oem = $('#oem').value.trim(); if (!oem) return;
  $('#status').textContent = 'Consultando BMV.parts...';
  try { const response = await fetch(`/api/external-catalog/import/${encodeURIComponent(oem)}`, { method: 'POST' }); const data = await response.json(); if (!response.ok) throw new Error(data.detail || 'Peca nao encontrada no BMV.parts.'); await loadListing(data.oemPartNumber); } catch (error) { $('#status').textContent = error.message; }
});
$('#realoem-button').addEventListener('click', async () => { const oem = $('#oem').value.trim(); if (!oem) return; window.open('https://www.realoem.com/bmw/enUS/select', 'realoem_lookup'); try { await navigator.clipboard.writeText(oem); } catch { } $('#realoem-dialog').showModal(); });

function renderVehicleCards(vehicles) {
  currentVehicles = new Map(vehicles.map(vehicle => [vehicle.id, vehicle]));
  $('#vehicle-cards').innerHTML = vehicles.length ? vehicles.map(vehicle => `<button type="button" class="vehicle-card" data-vehicle-id="${vehicle.id}"><img src="${escapeHtml(vehicle.imageUrl || fallbackVehicleImage)}" alt="${escapeHtml(vehicleName(vehicle))}" onerror="this.onerror=null;this.src='${fallbackVehicleImage}'"><span class="vehicle-card-body"><h3>${escapeHtml(vehicleName(vehicle))}</h3><p>${escapeHtml(vehicleDetail(vehicle))}</p><small>Mercado ${vehicle.marketRelevance ?? 0} | ${vehicle.partCount.toLocaleString('pt-BR')} pecas</small></span></button>`).join('') : '<p>Nenhum veiculo encontrado.</p>';
}
async function loadVehicleCards() {
  $('#vehicle-status').textContent = 'Carregando veiculos...';
  try { const response = await fetch(`/api/vehicle-catalog?take=24&search=${encodeURIComponent($('#vehicle-search').value.trim())}`); const data = await response.json(); if (!response.ok) throw new Error(data.detail || 'Nao foi possivel carregar os veiculos.'); renderVehicleCards(data); $('#vehicle-status').textContent = `${data.length} modelos exibidos.`; } catch (error) { $('#vehicle-status').textContent = error.message; }
}
async function loadVehicleParts(vehicleId, page = 0) {
  const vehicle = currentVehicles.get(vehicleId); if (!vehicle) return;
  currentVehicleId = vehicleId; currentVehiclePage = page; $('#vehicle-parts').hidden = false; $('#vehicle-parts-title').textContent = vehicleName(vehicle); $('#vehicle-parts-status').textContent = 'Carregando pecas...';
  try {
    const skip = page * vehiclePageSize; const response = await fetch(`/api/vehicle-catalog/${vehicleId}/parts?take=${vehiclePageSize}&skip=${skip}&search=${encodeURIComponent($('#vehicle-part-search').value.trim())}`); const data = await response.json(); if (!response.ok) throw new Error(data.detail || 'Nao foi possivel carregar as pecas.');
    const pages = Math.max(1, Math.ceil(data.total / vehiclePageSize));
    $('#vehicle-parts-status').textContent = `${data.total.toLocaleString('pt-BR')} pecas comerciais.`;
    $('#vehicle-parts-rows').innerHTML = data.items.map(part => `<tr><td>${escapeHtml(part.oemPartNumber)}</td><td>${escapeHtml(part.description)}</td><td>${escapeHtml(part.category)}</td><td>${escapeHtml([part.model, part.chassis, part.engine].filter(Boolean).join(' | '))}</td><td><button type="button" data-oem="${escapeHtml(part.oemPartNumber)}">Abrir OEM</button></td></tr>`).join('');
    $('#vehicle-page-info').textContent = `Pagina ${page + 1} de ${pages}`; $('#vehicle-page-prev').disabled = page === 0; $('#vehicle-page-next').disabled = page >= pages - 1;
  } catch (error) { $('#vehicle-parts-status').textContent = error.message; }
}
$('#vehicle-search').addEventListener('input', () => { clearTimeout(vehicleSearchTimer); vehicleSearchTimer = setTimeout(loadVehicleCards, 250); });
$('#vehicle-cards').addEventListener('click', event => { const card = event.target.closest('[data-vehicle-id]'); if (card) loadVehicleParts(Number(card.dataset.vehicleId)); });
$('#vehicle-parts-rows').addEventListener('click', event => { const button = event.target.closest('[data-oem]'); if (!button) return; $('#oem').value = button.dataset.oem; activateView('oem-workspace'); loadListing(button.dataset.oem); });
$('#vehicle-part-search').addEventListener('input', () => { clearTimeout(vehiclePartSearchTimer); vehiclePartSearchTimer = setTimeout(() => { if (currentVehicleId) loadVehicleParts(currentVehicleId, 0); }, 250); });
$('#vehicle-page-prev').addEventListener('click', () => { if (currentVehicleId && currentVehiclePage > 0) loadVehicleParts(currentVehicleId, currentVehiclePage - 1); });
$('#vehicle-page-next').addEventListener('click', () => { if (currentVehicleId) loadVehicleParts(currentVehicleId, currentVehiclePage + 1); });
$('#close-vehicle-parts').addEventListener('click', () => { $('#vehicle-parts').hidden = true; });

async function loadCategories() {
  if (categoriesLoaded) return;
  $('#category-status').textContent = 'Carregando categorias...';
  try {
    const response = await fetch('/api/parts/categories');
    const categories = await response.json();
    if (!response.ok) throw new Error(categories.detail || 'Nao foi possivel carregar as categorias.');
    $('#category-cards').innerHTML = categories.map(category => `<button type="button" class="category-card" data-category="${escapeHtml(category.category)}"><strong>${escapeHtml(category.category)}</strong><small>${category.partCount.toLocaleString('pt-BR')} pecas cadastradas</small></button>`).join('');
    $('#category-status').textContent = `${categories.length} categorias disponiveis.`;
    categoriesLoaded = true;
  } catch (error) { $('#category-status').textContent = error.message; }
}
async function loadCategoryParts(category) {
  $('#category-parts').hidden = false;
  $('#category-parts-title').textContent = category;
  $('#category-parts-rows').innerHTML = '<tr><td colspan="3">Carregando pecas...</td></tr>';
  try {
    const response = await fetch(`/api/parts/categories/${encodeURIComponent(category)}`);
    const parts = await response.json();
    if (!response.ok) throw new Error(parts.detail || 'Nao foi possivel carregar as pecas.');
    $('#category-parts-rows').innerHTML = parts.map(part => `<tr><td>${escapeHtml(part.oemPartNumber)}</td><td>${escapeHtml(part.description)}</td><td><button type="button" data-oem="${escapeHtml(part.oemPartNumber)}">Abrir OEM</button></td></tr>`).join('');
  } catch (error) { $('#category-parts-rows').innerHTML = `<tr><td colspan="3">${escapeHtml(error.message)}</td></tr>`; }
}
$('#category-cards').addEventListener('click', event => { const card = event.target.closest('[data-category]'); if (card) loadCategoryParts(card.dataset.category); });
$('#category-parts-rows').addEventListener('click', event => { const button = event.target.closest('[data-oem]'); if (!button) return; $('#oem').value = button.dataset.oem; activateView('oem-workspace'); loadListing(button.dataset.oem); });
$('#close-category-parts').addEventListener('click', () => { $('#category-parts').hidden = true; });

loadListing($('#oem').value);
