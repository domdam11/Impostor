document.addEventListener('DOMContentLoaded', () => {

    const app = new Framework7({
        root: '#app',
        name: 'Among Us Strategic',
        routes: [
            {
                path: '/',
                el: '.page[data-name="home"]'
            },
            {
                path: '/session/',
                el: '.page[data-name="session"]'
            }
        ]
    });

    window.loadSession = async function () {
        const sessionId = document.getElementById('session-id').value.trim();
        if (!sessionId) {
            app.dialog.alert('Please enter a session ID');
            return;
        }

        app.views.main.router.navigate('/session/');

        document.getElementById('session-title').textContent = `Session ${sessionId}`;
        document.getElementById('session-table').innerHTML = '<div class="block">Loading data...</div>';

        try {
            const response = await fetch(`http://localhost:5001/api/strategic/session/${sessionId}/data`);
            if (!response.ok) throw new Error('Not found');
            const res = await response.json();

            const columns = res.columns;
            const rows = res.data;

            let html = '<table class="data-table"><thead><tr>';
            html += columns.map(col => `<th>${col}</th>`).join('');
            html += '</tr></thead><tbody>';
            for (const row of rows) {
                const cls = `event-${row.eventType}`;
                html += `<tr class="${cls}">` + columns.map(c => `<td>${row[c]}</td>`).join('') + '</tr>';
            }
            html += '</tbody></table>';
            document.getElementById('session-table').innerHTML = html;

            window._csvData = { columns, rows };

        } catch (err) {
            document.getElementById('session-table').innerHTML = '<div class="block text-color-red">Session not found</div>';
        }
    };

    window.exportCSV = function () {
        const { columns, rows } = window._csvData || {};
        if (!rows) {
            app.dialog.alert('No data to export');
            return;
        }

        let csv = columns.join(',') + '\n';
        rows.forEach(row => {
            csv += columns.map(c => `"${row[c]}"`).join(',') + '\n';
        });

        const blob = new Blob([csv], { type: 'text/csv' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'session.csv';
        a.click();
        URL.revokeObjectURL(url);
    };
});
