# BiblioTech - Sistema di Gestione Prestiti Librari

Progetto scolastico per la gestione informatizzata dei prestiti della biblioteca di istituto.  
Il sistema sostituisce il vecchio registro cartaceo con un'applicazione web che permette agli studenti di richiedere prestiti e ai bibliotecari di gestire le restituzioni.

---

## 📁 Struttura del progetto
```
BIBLIOTECH/
│
├── docker-compose.yml # Configurazione Docker per avviare web server e database
├── Dockerfile # Immagine Docker personalizzata con PHP e mysqli
├── README.md # Questo file
│
├── docs/
│ └── analisi_progetto.pdf # Documentazione completa (ER, UML, sicurezza)
│
├── sql/
│ └── database.sql # Script per creare database, tabelle e dati di test
│
└── src/ # Codice sorgente PHP dell'applicazione
├── config.php # Connessione al database MySQL
├── login.php # Pagina di login con autenticazione
├── logout.php # Logout e distruzione sessione
├── libri.php # Catalogo libri con ricerca e filtri
├── libro.php # Dettaglio di un singolo libro
├── presta_libro.php # Gestisce la richiesta di prestito (studente)
├── prestiti.php # Elenco prestiti attivi dello studente
└── gestione_restituzioni.php # Pannello bibliotecario per restituzioni
```
---

## 🛠️ Tecnologie utilizzate

- **PHP 8.2** con estensione `mysqli`
- **MySQL 8.0** per il database
- **Docker & Docker Compose** per l'ambiente di sviluppo
- **HTML/CSS** per l'interfaccia utente
- **Git** per il versionamento

---

## ⚙️ Requisiti

Prima di iniziare, assicurati di avere installato:

- [Docker Desktop](https://www.docker.com/products/docker-desktop) (include Docker Compose)
- [Git](https://git-scm.com/downloads)

---

## 🚀 Come avviare il progetto

### 1. Clona il repository
```
git clone https://github.com/TUO_USERNAME/bibliotech.git
cd bibliotech
```
### 2. Avvia i container Docker
```
docker-compose up -d
```
Questo comando:
- Crea e avvia il container web (PHP + Apache)
- Crea e avvia il container db (MySQL)
- Importa automaticamente il file sql/database.sql nel database

Aspetta 30-40 secondi la prima volta (MySQL deve inizializzarsi).

### 3. Accedi all'applicazione
Apri il browser e vai su:
```
http://localhost:8080/login.php
```
### 4. Ferma i container (quando hai finito)
```
docker-compose down
```
## 👥 Utenti di test
Tutti gli utenti hanno la stessa password: password

Studenti
- mario.rossi@example.com
- luca.bianchi@example.com
- anna.verdi@example.com

Bibliotecario
- paola.neri@example.com

## 📖 Come usare l'applicazione
### Come STUDENTE
1. Login
Vai su http://localhost:8080/login.php e inserisci email e password di uno studente.

2. Visualizza catalogo
Dopo il login verrai reindirizzato alla pagina libri.php dove vedi tutti i libri disponibili.

3. Cerca libri
Usa la barra di ricerca per filtrare per titolo o autore.
Seleziona "Solo disponibili" per vedere solo i libri con copie disponibili.

4. Richiedi prestito
Clicca su "PRENDI IN PRESTITO" accanto al libro desiderato.
Il sistema:
- Registra il prestito con la data odierna
- Decrementa le copie disponibili
- Ti reindirizza alla pagina "I miei prestiti"
- Limite: massimo 3 prestiti contemporanei per studente.

5. Visualizza i tuoi prestiti
Clicca su "→ Visualizza i miei prestiti attivi" per vedere l'elenco dei libri che hai in prestito.

6. Logout
Clicca su "Logout" in alto a destra.

### Come BIBLIOTECARIO
1. Login
Vai su http://localhost:8080/login.php e inserisci email e password del bibliotecario.

2. Gestione restituzioni
Dopo il login verrai reindirizzato alla pagina gestione_restituzioni.php.
Qui vedi tutti i prestiti attivi con:
- Titolo del libro
- Nome dello studente
- Data di inizio prestito

3. Registra restituzione
Clicca su "RESTITUISCI" accanto al prestito da chiudere.
Il sistema:
- Imposta la data di fine prestito
- Incrementa le copie disponibili del libro

4. Visualizza catalogo
Clicca su "Vai al catalogo libri" per vedere tutti i libri.
Come bibliotecario puoi vedere anche le note interne sui libri.

5. Logout
Clicca su "Logout" in alto a destra.

## 🔒 Sicurezza implementata
- Password hashate: le password sono salvate nel database solo come hash (funzione password_hash di PHP), mai in chiaro
- Prepared statements: tutte le query SQL usano mysqli_prepare per prevenire SQL injection
- Controllo sessioni: ogni pagina protetta verifica che l'utente sia autenticato
- Controllo ruoli: le pagine amministrative sono accessibili solo ai bibliotecari

## 🎨 Funzionalità aggiuntive
Ricerca e filtri nel catalogo
Barra di ricerca per titolo/autore + checkbox "Solo disponibili"

Limite prestiti: Ogni studente può avere massimo 3 prestiti attivi contemporaneamente

📚 Documentazione
La documentazione completa del progetto si trova in docs/analisi_progetto.pdf e include:
- Descrizione del sistema
- Diagramma Entità-Relazioni (ER)
- Diagramma UML delle classi
- Specifiche di sessione e sicurezza
- Progettazione database

## 👨‍💻 Autore
Alessandro Casadibari
Progetto realizzato per il corso di Informatica - Classe 5° Superiore
Email: casadibari.alessandro@panettipitagora.edu.it

## 📝 Licenza
Progetto didattico - uso educativo
