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
        showErrors(['Please enter path to the catalog.']);
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

        populateList('listAdded', 'countAdded', report.added);
        populateList('listModified', 'countModified', report.modified);
        populateList('listDeleted', 'countDeleted', report.deleted);

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
        li.style.background = 'transparent';
        li.style.color = '#64748b';
        li.style.fontStyle = 'italic';
        li.textContent = 'Changes weren\'t detected';
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