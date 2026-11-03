# Audio Manager (por eventos)

Motor de audio para Unity pensado para que **el equipo de sonido trabaje sin tocar código ni saber Unity**.
Nació en *Archaeo* y se ha separado para poder usarlo en cualquier proyecto.

## La idea

El código del juego nunca conoce los archivos de audio. Solo dice **"ha pasado X"**:

```csharp
GameAudio.Play(AudioIds.UI_Click);
GameAudio.Music(AudioIds.Music_Main);
```

Cada evento es un asset con sus sonidos y ajustes (volumen y tono en dB y semitonos, variaciones al azar, tiempo mínimo
entre repeticiones, copias simultáneas, prioridad, bajada automática de la música...). Quien hace el audio los rellena;
el código no cambia.

## Qué incluye

**Motor (Runtime)**
- Reserva fija de voces con prioridad, límite de copias por evento y tiempo mínimo entre repeticiones.
- Bancos de volumen (Música, Efectos, Interfaz, Ambiente) más maestro, guardados entre sesiones.
- Ducking: la música baja sola cuando suena un sonido importante.
- Música con fundido cruzado, intro opcional + bucle con arranque preciso por reloj de audio.
- Sonidos en bucle con control de volumen y fundidos; elección de clip al azar sin repetir, o en orden.
- Silencio automático con la ventana sin foco, configurable por evento, por pista y global.
- Sin dependencia de `AudioMixer` (se puede conectar uno por banco, opcionalmente).

**Herramientas (Editor)** — menú `Tools > Audio Manager`
- **Catálogo** como asset, con exportación e importación CSV (se edita en una hoja de cálculo).
- Genera los assets de cada evento y pista, la biblioteca, el archivo `AudioIds.cs` y la guía para el equipo de sonido.
- **Asignación automática de sonidos por nombre de archivo**: `UI_Click_01.wav` va al evento `UI_Click` sola.
- **Reglas de importación por carpeta** (música en streaming, efectos comprimidos...), aplicadas solo la primera vez.
- Botón **▶ Probar** en cada evento y **Panel de pruebas** con el estado de todos los sonidos.
- Informe de cobertura: qué eventos siguen sin sonido.

**Kit de inicio (Samples)**: sonido automático en todos los botones, deslizadores de volumen y música por estados.

## Instalación

Requiere Unity 2022.3 o superior. No tiene dependencias.

**Desde un repositorio Git** (lo recomendado): `Window > Package Manager > + > Add package from git URL` y pega
`https://github.com/TU-USUARIO/TU-REPO.git#v1.0.0` (la parte `#v1.0.0` fija la versión). Hace falta tener Git instalado.
Si el paquete está dentro de una subcarpeta del repositorio: `https://...git?path=/com.studio.audio-manager#v1.0.0`.

**Desde disco** (para probar o desarrollar): `Package Manager > + > Add package from disk...` y elige `package.json`.

**Incrustado**: copia la carpeta entera dentro de la carpeta `Packages/` del proyecto.

Para el kit de inicio: `Package Manager > Audio Manager > Samples > Importar`.

## Empezar un proyecto nuevo

1. `Tools > Audio Manager > 0. Crear el catálogo`. Crea `Assets/Audio/AudioCatalog.asset`.
2. Rellena la lista de **eventos** y **pistas** en el Inspector, o impórtala desde un CSV (**Importar desde CSV...**).
3. Pulsa, en orden, los botones del catálogo:
   1. *Crear o completar los sonidos del catálogo* (crea los assets y la biblioteca `AudioLibrary`).
   2. *Asignar sonidos por nombre de archivo*.
   3. *Generar AudioIds.cs* (las constantes para el código).
   4. *Generar la guía para el equipo de sonido* (`LEEME_Audio.md`).
4. En tu escena inicial, crea un objeto vacío con el componente **AudioManager** y asígnale la `AudioLibrary`.
5. Asegúrate de que hay un **Audio Listener** en la cámara. (Opcional: en el AudioManager, el campo **Music On Start** pone una pista al arrancar.)
6. Desde tu código: `GameAudio.Play(AudioIds.LoQueSea);`
7. Pásale al equipo de sonido la carpeta `Assets/Audio/` con `LEEME_Audio.md` dentro.

Cuando añadas eventos nuevos: edita el catálogo y repite el paso 3. No se pierde ningún sonido ya asignado.

## Uso desde el código

```csharp
GameAudio.Play("UI_Click");                       // un sonido
AudioHandle h = GameAudio.Play(AudioIds.Scrub);   // un bucle
GameAudio.SetVolume(h, 0.5f);                     // cambia su volumen mientras suena
GameAudio.Stop(h);                                // lo para (con el fundido de salida del evento)
GameAudio.Music(AudioIds.Music_Main);             // cambia de pista con fundido cruzado
GameAudio.Has(AudioIds.X);                        // ¿ya tiene algún sonido?  (también: HasMusic, IsPlaying)
```

Todas las llamadas son seguras: si no hay `AudioManager` en la escena o el evento aún no tiene sonidos, simplemente no suenan.
Mientras se produce el audio, el juego funciona en silencio sin errores.

Volúmenes: `AudioManager.Instance.SetMasterVolume(v)` y `SetBusVolume(AudioBus.Music, v)` (se guardan solos).

## Para el equipo de sonido

Todo lo que necesitan está en la guía que genera el paquete (`LEEME_Audio.md`): nombres de archivo, carpetas, especificaciones
técnicas y la lista de eventos con cuándo suena cada uno.

## Qué se queda en cada juego

El paquete es genérico. Lo que depende de tu juego se escribe en tu proyecto, en un solo sitio:
- **El catálogo** (qué sonidos existen).
- **Las conexiones** "cuando pasa X, suena Y": un script tuyo que se suscribe a los eventos de tu juego y llama a `GameAudio.Play`.
- **La música por contexto**: ver `StateMusicDirector` en el kit de inicio.

## Estructura

```
com.studio.audio-manager/
  package.json
  Runtime/    AudioManager, SoundEvent, MusicTrack, AudioLibrary, AudioCatalogAsset, GameAudio...
  Editor/     catálogo, generadores, CSV, asignación por nombre, importación, inspectores, panel de pruebas
  Samples~/   Kit de inicio
```

## Versiones

Cambios en `CHANGELOG.md`. Para publicar una versión: sube el `version` de `package.json`, haz commit y crea una etiqueta
(`git tag v1.1.0 && git push --tags`). Cada proyecto actualiza cambiando el `#v1.1.0` de su enlace en `Packages/manifest.json`.

**Importante:** no borres los archivos `.meta` del paquete. Las escenas guardan referencias a los scripts por el identificador
(GUID) que está en ellos; si cambia, los componentes se quedan "missing".

## Renombrar el paquete

El nombre `com.studio.audio-manager`, el autor y el espacio de nombres `Studio.Audio` son genéricos. Para ponerle el de tu estudio,
cambia `name` y `author` en `package.json`, y haz una sustitución global de `Studio.Audio` por tu espacio de nombres
(y `Studio.Audio.Runtime` / `Studio.Audio.Editor` en los `.asmdef`). Hazlo antes de usarlo en el primer proyecto: después cuesta más.
