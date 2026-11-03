const $ = id => document.getElementById(id);
let rooms = [];
let loadVersion = 0;
function message(text, error = false) {
    $('message').textContent = text;
    $('message').className = error ? 'error' : 'success';
}
async function api(path, options) {
    const response = await fetch(path, options);
    if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        throw new Error(problem.errors ? Object.values(problem.errors).flat().join(' ') : problem.detail || 'Request failed. Please try again.');
    }
    return response.status === 204 ? null : response.json();
}
function element(tag, text, className) {
    const node = document.createElement(tag);
    if (text !== undefined) node.textContent = text;
    if (className) node.className = className;
    return node;
}
async function loadBookings() {
    const version = ++loadVersion;
    const bookings = await api('/api/bookings?day=' + encodeURIComponent($('day').value));
    if (version !== loadVersion) return;
    $('schedule').replaceChildren();
    for (const room of rooms) {
        const card = element('article', undefined, 'room-card');
        card.append(element('h3', room.name));
        const matches = bookings.filter(b => b.roomId === room.id);
        if (!matches.length) card.append(element('p', 'No bookings. This room is available.', 'empty'));
        for (const booking of matches) {
            const row = element('div', undefined, 'booking');
            const details = element('div');
            details.append(element('p', booking.name), element('small', booking.startUtc.slice(11,16) + '–' + booking.endUtc.slice(11,16) + ' UTC'));
            const cancel = element('button', 'Cancel', 'cancel');
            cancel.type = 'button';
            cancel.setAttribute('aria-label', 'Cancel booking for ' + booking.name);
            cancel.addEventListener('click', async () => {
                cancel.disabled = true;
                try { await api('/api/bookings/' + booking.id, {method:'DELETE'}); message('Booking cancelled.'); await loadBookings(); }
                catch (error) { message(error.message, true); cancel.disabled = false; }
            });
            row.append(details, cancel); card.append(row);
        }
        $('schedule').append(card);
    }
}
$('booking-form').addEventListener('submit', async event => {
    event.preventDefault(); $('book').disabled = true;
    try {
        const day = $('day').value;
        await api('/api/bookings', {method:'POST', headers:{'Content-Type':'application/json'},body:JSON.stringify({roomId:Number($('room').value),name:$('name').value.trim(),startUtc:day+'T'+$('start').value+':00Z',endUtc:day+'T'+$('end').value+':00Z'})});
        message('Room booked successfully.'); await loadBookings();
    } catch (error) { message(error.message,true); }
    finally { $('book').disabled = false; }
});
$('day').addEventListener('change', () => loadBookings().catch(error => message(error.message,true)));
$('refresh').addEventListener('click', () => loadBookings().catch(error => message(error.message,true)));
async function initialize() {
    $('day').value = new Date().toISOString().slice(0,10);
    try {
        rooms = await api('/api/rooms');
        for (const room of rooms) { const option = element('option',room.name); option.value = room.id; $('room').append(option); }
        await loadBookings();
    } catch (error) { message(error.message,true); }
}
initialize();
