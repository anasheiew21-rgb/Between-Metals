# Regenera los .wav del juego a partir de GeneradorAudio.cs.
#
# Se puede correr con el Editor de Unity abierto: escribe los archivos y Unity los reimporta al
# recuperar el foco. Los .wav salen identicos byte a byte en cada corrida (semillas fijas), asi
# que correrlo de gusto NO ensucia el git status.
#
#   powershell -ExecutionPolicy Bypass -File Tools\GeneradorAudio\generar.ps1
#
# Destino por defecto: Assets\Audio\Resources (BibliotecaDeSonidos los carga desde ahi por nombre).

param(
    [string]$Destino
)

$ErrorActionPreference = 'Stop'

$raizProyecto = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $Destino) {
    $Destino = Join-Path $raizProyecto 'Assets\Audio\Resources'
}

$fuente = Join-Path $PSScriptRoot 'GeneradorAudio.cs'
if (-not (Test-Path $fuente)) { throw "No se encontro $fuente" }

Write-Host "Compilando GeneradorAudio.cs..."
Add-Type -Path $fuente -ErrorAction Stop

Write-Host "Generando audio en $Destino"
Write-Host ""
[BetweenMetals.Tools.GeneradorAudio]::Generar($Destino)
