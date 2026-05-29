document.addEventListener('DOMContentLoaded', () => {
    const btnAnalyze = document.getElementById('btnAnalyze');
    btnAnalyze.addEventListener('click', executeAnalysis);
});

async function executeAnalysis() {
    const dirPathInput = document.getElementById('directoryPath');
    const loader = document.getElementById('loader');
    const resultsSection = document.getElementById('resultsSection');
    const errorMessage = document.getElementById('errorMessage');

    errorMessage.classList.add('hidden');
    errorMessage.innerHTML = '';
    resultsSection.classList.add('hidden');

    const pathValue = dirPathInput.value.trim();
    if (!pathValue) {
        showErrors(['Please enter an absolute directory path to analyze.']);
        return;
    }

    loader.classList.remove('hidden');

    try {
        const response = await fetch('/api/analyze', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ directoryPath: pathValue })
        });

        if (!response.ok) {
            const errorData = await response.json();
            if (errorData.errors && Array.isArray(errorData.errors)) {
                throw new ValidationError(errorData.errors);
            } else {
                throw new Error(errorData.detail || 'Server communication error.');
            }
        }

        const report = await response.json();

        // Update summary values
        document.getElementById('valDuration').textContent = `${report.scanDurationMs.toFixed(2)} ms`;
        
        // Format timestamp cleanly
        const scanDate = new Date(report.scanTimestampUtc);
        const formattedDate = scanDate.toISOString().replace('T', ' ').substring(0, 19) + ' UTC';
        document.getElementById('valTimestamp').textContent = formattedDate;

        // Update status banner
        const statusBanner = document.getElementById('scanStatusBanner');
        if (report.isPartial) {
            statusBanner.className = 'status-banner status-warning';
            statusBanner.innerHTML = '⚠️ <strong>Partial Scan:</strong> Some folders or files could not be read. Deleted items inside skipped directories are preserved in the snapshot to avoid data loss.';
        } else {
            statusBanner.className = 'status-banner status-success';
            statusBanner.innerHTML = '✓ <strong>Scan Complete:</strong> Successfully scanned all directories and verified file states.';
        }

        // Populate lists
        populateList('listAdded', 'countAdded', report.added);
        populateList('listModified', 'countModified', report.modified);
        populateList('listMetadata', 'countMetadata', report.metadataChanged);
        populateList('listRemoved', 'countRemoved', report.removed);
        populateList('listRemovedDirs', 'countRemovedDirs', report.removedDirectories);
        populateList('listSkippedDirs', 'countSkippedDirs', report.skippedDirectories);
        populateList('listSkippedFiles', 'countSkippedFiles', report.skippedFiles);
        populateList('listUnstable', 'countUnstable', report.unstableFiles);

        resultsSection.classList.remove('hidden');
    } catch (error) {
        if (error instanceof ValidationError) {
            showErrors(error.messages);
        } else {
            showErrors([error.message]);
        }
    } finally {
        loader.classList.add('hidden');
    }
}

function populateList(listId, countId, items) {
    const listElement = document.getElementById(listId);
    const countElement = document.getElementById(countId);

    countElement.textContent = items.length;
    listElement.innerHTML = '';

    if (items.length === 0) {
        const li = document.createElement('li');
        li.className = 'empty-placeholder';
        li.textContent = 'No changes detected';
        listElement.appendChild(li);
        return;
    }

    items.forEach(item => {
        const li = document.createElement('li');
        li.textContent = item;
        listElement.appendChild(li);
    });
}

function showErrors(messages) {
    const errorMessage = document.getElementById('errorMessage');
    errorMessage.innerHTML = '';

    messages.forEach(msg => {
        const div = document.createElement('div');
        div.textContent = `• ${msg}`;
        errorMessage.appendChild(div);
    });

    errorMessage.classList.remove('hidden');
}

class ValidationError extends Error {
    constructor(messages) {
        super('Validation failed');
        this.messages = messages;
    }
}