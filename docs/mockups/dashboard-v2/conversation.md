# Revisione dashboard GOLP

Data: 10 luglio 2026

## Richiesta iniziale

**Utente**

> Guarda la dashboard mi sembra caotica e mancante delle informazioni necessarie, puoi fare uno sforzo di revisione per capire come può migliorare graficamente e funzionalmente? Puoi fare un mockup per darmi l'idea di come verrebbe?

**Codex**

La dashboard esistente è principalmente un elenco di collegamenti e non mostra lo stato reale del giocatore. È stato quindi preparato un mockup mobile-first che organizza i contenuti secondo questa gerarchia:

1. Azioni urgenti: risultati da confermare o contestazioni.
2. Circolo attivo: contesto di rating e statistiche.
3. Situazione personale: rating, posizione, percentuale di vittorie e serie positiva.
4. Ultime partite: risultato e variazione del rating.
5. Navigazione persistente: Home, Partite, Circoli, Profilo e registrazione partita.

L'arancione è riservato alle azioni primarie; verde e rosso descrivono gli esiti. Il mockup usa informazioni già presenti nel dominio GOLP. Per l'implementazione reale è consigliato un endpoint aggregato dedicato alla dashboard.

File prodotti:

- [Mockup HTML](index.html)
- [Preview PNG](preview.png)
- [Note progettuali](README.md)

## Pulsante centrale e navigazione inferiore

**Utente**

> Cosa fa il pulsante [più] in basso? Inoltre il menù in basso lo terresti per tutte le pagine?

**Codex**

Il pulsante `+` è la scorciatoia principale per **Registra partita**. Nel mockup era presente anche il pulsante `+ Partita` nell'intestazione, ma nella versione finale è preferibile evitare la duplicazione e mantenere il pulsante centrale, sempre raggiungibile con il pollice.

È opportuno aggiungere sotto l'icona l'etichetta **Registra**, in modo che la funzione sia comprensibile anche al primo utilizzo.

La navigazione inferiore dovrebbe essere presente nelle aree principali:

- Dashboard
- Partite
- Circoli
- Profilo

Non dovrebbe invece apparire nei flussi focalizzati:

- registrazione o conferma di una partita;
- creazione o modifica di un circolo;
- login e registrazione;
- procedure guidate e schermate modali;
- pagine pubbliche aperte tramite invito.

In questi casi è preferibile una barra superiore con pulsante Indietro e titolo. Il principio adottato è: **navigazione inferiore per spostarsi tra le aree; intestazione per muoversi all'interno di un'attività**.

## Gestione di più circoli

**Utente**

> Come verrebbe se uno gioca in più circoli?

**Codex**

Con più circoli, il selettore **Circolo attivo** diventa il filtro principale della dashboard. Toccandolo si apre un pannello dal basso con una voce generale e l'elenco dei circoli, per esempio:

- **Tutti i circoli** — riepilogo complessivo;
- **Padel Club Roma** — Padel, rating 1.142, posizione #3;
- **Beach Arena Ostia** — Beach tennis, rating 1.087, posizione #7;
- **Amici del Martedì** — Padel, rating 1.205, posizione #2.

Il circolo selezionato determina:

- rating, posizione e statistiche;
- classifica aperta dal relativo collegamento;
- partite recenti;
- simulatore coerente con il metodo di rating;
- destinazione predefinita di `Registra partita`.

Le richieste urgenti restano invece globali e mostrano chiaramente il circolo di appartenenza. In questo modo una conferma importante non scompare quando è selezionato un altro circolo.

Nella modalità **Tutti i circoli** i rating non vengono sommati, perché appartengono a classifiche, sport o metodi differenti. La card principale mostra invece:

- numero di circoli attivi;
- partite totali;
- percentuale di vittorie;
- risultati da confermare;
- una card sintetica per ogni circolo con rating e posizione.

L'ultimo circolo selezionato viene memorizzato. Con più di circa 5–6 circoli, il pannello di selezione dovrebbe offrire anche ricerca e preferiti.

## Decisioni emerse

- Trasformare la dashboard da menu di link a centro operativo personale.
- Dare priorità alle attività che richiedono attenzione.
- Mantenere un contesto di circolo esplicito.
- Non aggregare rating appartenenti a classifiche differenti.
- Rendere globali le notifiche e le conferme urgenti.
- Usare una navigazione inferiore persistente solo nelle aree principali.
- Mantenere una sola azione primaria `Registra partita`, con icona ed etichetta.
