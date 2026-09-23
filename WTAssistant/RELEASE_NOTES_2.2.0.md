# WT VR Assistant 2.2.0 - Custom Aircraft Controls local test

- Default Profile is now the live Default Controls template.
- Automatically created aircraft profiles start by following Default Controls.
- Double-click an aircraft card to open the new aircraft-specific controls editor.
- Aircraft editor uses War Thunder Wiki aircraft metadata/artwork and country background assets when available.
- Each aircraft can switch between Default Controls and Custom Controls without deleting its previous custom snapshot.
- Custom Controls include aircraft-specific trim/HAT bindings, trim rate/steps, axis curves and Flight Assistant configuration.
- Flight Assistant is limited to custom prop profiles and custom jets explicitly marked as having no in-game damping/SAS. It is disabled for helicopters, default-controlled aircraft, damping/SAS jets and unknown jets.
- Copy/Paste Aircraft Settings is restricted to compatible classes (prop->prop, no-damping jet->no-damping jet, damping jet->damping jet, helicopter->helicopter).
- Existing aircraft profiles migrate as Custom Controls so old tuned settings are preserved.
- Physical controller axis routing remains global.
