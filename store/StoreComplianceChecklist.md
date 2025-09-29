# Checklist di conformità per Microsoft Store

Questa checklist riassume i controlli da eseguire prima di inviare **Screenshot Flash** alla certificazione di Microsoft Store.

## 1. Identità e firma
- [ ] Associa il pacchetto allo Store da **Visual Studio > Store > Associa App con lo Store...** per aggiornare `Identity` e certificato.
- [ ] Imposta il numero di versione in `Package.appxmanifest` seguendo lo schema `Major.Minor.Build.Revision` e incrementalo a ogni submission.
- [ ] Genera un pacchetto `MSIX` o `MSIX Upload` con firma valida (certificato rilasciato da Partner Center o da autorità attendibile).

## 2. Compatibilità tecnica
- [ ] Verifica in `Release | x64` e, se necessario, in `Release | x86` che l'applicazione venga eseguita senza eccezioni.
- [ ] Conferma che il pacchetto dichiari solo le funzionalità richieste (`runFullTrust`) e che l'app non necessiti privilegi amministrativi.
- [ ] Esegui i test automatizzati di certificazione tramite **Windows App Certification Kit (WACK)** sul pacchetto generato.

## 3. Asset grafici
- [ ] Sostituisci i loghi segnaposto nella cartella `ScreenshotFlash.Package/Images` con risorse definitive (PNG con canale alpha) e rispetta le dimensioni ufficiali.
- [ ] Verifica che `Square44x44Logo`, `Square71x71Logo`, `Square150x150Logo`, `Square310x310Logo`, `Wide310x150Logo` e `SplashScreen` siano coerenti nel branding.
- [ ] Fornisci anche immagini promozionali aggiuntive richieste dalla dashboard (Store listings > Visual Assets).

## 4. Contenuti e policy
- [ ] Assicurati che l'app non contenga contenuti vietati (violenza, odio, materiale sessuale esplicito) in conformità con le [Microsoft Store Policies](https://learn.microsoft.com/windows/uwp/publish/store-policies).
- [ ] Includi nel pacchetto la documentazione aggiornata (`store/PrivacyPolicy.md`, `store/SupportPolicy.md`).
- [ ] Indica correttamente i diritti di utilizzo e gli eventuali attributi di licenza nel listino dello Store.

## 5. Documentazione e supporto
- [ ] Aggiorna la pagina **Descrizione** con testo in tutte le lingue supportate (almeno italiano e inglese) e allega screenshot recenti.
- [ ] Fornisci un contatto di supporto funzionante (email o URL) che risponda entro tempi ragionevoli.
- [ ] Collega la privacy policy pubblica (vedi file `store/PrivacyPolicy.md`) nell'app e nella dashboard.

## 6. Prezzi e disponibilità
- [ ] Imposta il prezzo base a **0,99 €** (o equivalente locale) nella sezione *Pricing & availability*.
- [ ] Definisci eventuali periodi di prova gratuita se desiderati (minimo 1 giorno, massimo 30 giorni).
- [ ] Controlla la disponibilità geografica e i mercati selezionati per evitare esclusioni indesiderate.

## 7. Verifiche finali
- [ ] Installa il pacchetto MSIX su un dispositivo di test pulito per validare l'esperienza di installazione/disinstallazione.
- [ ] Controlla che la cartella `%LOCALAPPDATA%\Packages` venga ripulita alla disinstallazione e che non restino file residui.
- [ ] Salva un report della sessione di test per eventuali audit o richieste di Microsoft.

> Suggerimento: conserva questa checklist in `store/` e contrassegna ogni voce durante la fase di revisione pre-submission.
