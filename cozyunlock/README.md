after the first launch with the mod, `BepInEx/config/mephila.cozyunlock.cfg` has a toggle for each part and the money amount. changes apply next launch.


to undo, delete `cozyunlock.dll`. the full game items lock again. unlocked levels and owned cosmetics are saved in your save, so those stay.


troubleshooting: check `BepInEx/LogOutput.log` for lines from `cozyunlock`. it logs how many items it opened, and warns if a game update renamed something.
