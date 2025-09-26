# RScreenFlash

RScreenFlash è una piccola utility WinForms per Windows che permette di:

- Catturare rapidamente lo schermo attivo con un effetto "flash" bordato.
- Eseguire catture parziali tenendo premuto **Shift** mentre si avvia il programma.
- Copiare l'immagine negli Appunti e salvarla automaticamente nella cartella `Immagini/Screenshots`.

## Funzionamento

1. Avvia l'applicazione (`ScreenshotFlash.exe`).
2. Se desideri catturare solo una porzione dello schermo, tieni premuto **Shift** prima di eseguire l'app: comparirà un overlay che permette di tracciare il rettangolo da acquisire su tutti i monitor.
3. Per la cattura a schermo intero, l'app evidenzia temporaneamente il monitor attivo con un bordo ciano semi-trasparente, quindi copia il risultato negli Appunti e lo salva su disco.
4. Gli screenshot vengono nominati automaticamente con il formato `screen_<numero progressivo>_<timestamp>.png`.

## Requisiti

- Windows 10 o successivo.
- .NET Framework 4.8 (o successivo compatibile con WinForms).

## Compilazione

1. Apri la soluzione `ScreenshotFlash.sln` in **Visual Studio**.
2. Seleziona la configurazione desiderata (ad es. `Release | x64`).
3. Esegui **Build > Build Solution** (`Ctrl+Shift+B`).
4. Il file eseguibile verrà generato nella cartella `bin/<Configurazione>/<Piattaforma>/` del progetto `ScreenshotFlash`.

## Percorso di salvataggio

Gli screenshot vengono salvati automaticamente nella cartella:

```
%USERPROFILE%\Pictures\Screenshots
```

Se la cartella non esiste, viene creata alla prima esecuzione.

## Suggerimenti

- Puoi creare un collegamento all'eseguibile e assegnargli una scorciatoia da tastiera per avviare rapidamente la cattura.
- Se devi condividere uno screenshot immediatamente, lo trovi già negli Appunti dopo l'acquisizione: basta incollarlo (`Ctrl+V`) in chat, email o editor di immagini.

## Licenza

Questo progetto viene fornito "as-is". Adatta il codice in base alle tue esigenze.
