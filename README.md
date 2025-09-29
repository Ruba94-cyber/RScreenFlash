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

## Distribuzione MSIX per Microsoft Store

Il repository include il progetto `ScreenshotFlash.Package`, pronto per generare un pacchetto MSIX (Desktop Bridge) da pubblicare su Microsoft Store.

### 1. Preparazione dell'identità

1. Apri la soluzione `ScreenshotFlash.sln` in **Visual Studio 2022** (o successivo).
2. Fai clic destro sul progetto **ScreenshotFlash.Package** > **Store** > **Associa App con lo Store...** e segui la procedura guidata.
   - Questa operazione aggiorna automaticamente `Package.appxmanifest` con `Identity` e certificato forniti da Partner Center.
   - Se stai testando in locale, puoi invece scegliere **Crea certificato di test** per generare un certificato autofirmato.

### 2. Generazione/Aggiornamento degli asset grafici

Per evitare il versionamento di file binari, le immagini nella cartella `ScreenshotFlash.Package/Images` vengono generate al momento della compilazione tramite lo script PowerShell [`tools/GenerateStoreAssets.ps1`](tools/GenerateStoreAssets.ps1).

1. Se desideri dei segnaposto veloci, esegui il comando (da PowerShell):

   ```powershell
   ./tools/GenerateStoreAssets.ps1 -Verbose
   ```

   Lo script crea i loghi minimi richiesti (44x44, 71x71, 150x150, 310x310, 310x150, 620x300 e StoreLogo 50x50) e viene invocato automaticamente dal progetto MSIX prima della build.
2. Per la pubblicazione reale, sostituisci i file generati con asset grafici coerenti con il tuo brand mantenendo le stesse dimensioni.

> Nota: l'eseguibile WinForms utilizza l'icona predefinita di Windows. Aggiungi il tuo file `.ico` personalizzato impostando `<ApplicationIcon>` nel progetto `ScreenshotFlash.csproj` prima di creare il pacchetto finale.

### 3. Compilazione del pacchetto

1. Imposta il progetto **ScreenshotFlash.Package** come progetto di avvio.
2. Seleziona la configurazione desiderata (`Release | x64` per lo Store, eventualmente `Release | x86` per supportare macchine a 32 bit).
3. Esegui **Build > Pubblica > Crea pacchetto app** e scegli `MSIX` come formato di output.
4. Il pacchetto firmato (`.msix` o `.msixupload`) verrà salvato nella cartella di destinazione scelta durante la procedura guidata.

### 4. Pubblicazione sullo Store

1. Accedi a [Partner Center](https://partner.microsoft.com/dashboard) e crea una nuova submission per l'app.
2. Carica il pacchetto `.msixupload` generato al punto precedente.
3. Compila i metadati (descrizione, screenshot, prezzo) e invia la submission per la certificazione.

Consulta la cartella [`store/`](store) per:

- [`Guida_Pubblicazione_99cent.txt`](store/Guida_Pubblicazione_99cent.txt): passaggi dettagliati per fissare il prezzo a 0,99 € e completare la submission.
- [`StoreComplianceChecklist.md`](store/StoreComplianceChecklist.md): elenco dei controlli richiesti dalle Microsoft Store Policies.
- [`PrivacyPolicy.md`](store/PrivacyPolicy.md) e [`SupportPolicy.md`](store/SupportPolicy.md): documenti da collegare nella scheda dello Store e all'interno dell'app.

Per installazioni manuali offline puoi comunque distribuire il file `.msix` generato, ricordandoti di fornire anche il certificato pubblico se non è stato firmato da un'autorità riconosciuta.

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
