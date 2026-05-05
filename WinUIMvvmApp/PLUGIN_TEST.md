# 🧪 Test Sistema Plugin

## Test 1: PluginLoader
- Carica plugin da ./plugins
- Usa AssemblyLoadContext isolato
- Registra comandi
- Gestisce errori

## Test 2: ShowFileSizeInMBPlugin
- Comando: Converti Dimensioni in MB
- Comando: Analizza Cartella  
- Comando: Pulisci Log
- Logga in Output

## Test 3: ContentDialog Errori
- Mostra errori caricamento
- Messaggi utente chiari
- Nessun crash

## Test 4: Menu Plugins
- Aggiorna dinamicamente
- Sottomenu per plugin
- Icone comandi
- Tooltip descrizioni

## Test 5: Isolamento
- Plugin non nel dominio principale
- Caricamento/Scaricamento pulito
- Dipendenze gestite
- Memoria liberata
