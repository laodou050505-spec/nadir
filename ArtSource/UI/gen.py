import json
base = "Straight-on flat scan of one old enamel hospital sign plate, fills the entire image edge to edge with no background and no perspective. Pale cream enamel with a thin dull teal border line. Worn, faded, light scratches, slightly chipped corners, clean, no rust stains. Evenly lit. Absolutely no letters, no words, no numbers. In the exact center, large, one simple solid black pictogram in the international safety-sign style: "
icons = {
 "h_step": "a walking person stepping off a square floor tile that has a jagged crack in it, arrow pointing away from the crack",
 "h_gaze": "a single human eye above a hand whose thumb and index finger are pinching together",
 "h_over": "a single foot taking a long step across a square gap in the floor",
 "h_swat": "an open hand swinging sideways with three curved motion lines behind it",
 "h_push": "an open palm facing forward with the fingers up, pushing, with three short straight lines in front of the palm",
 "h_eye": "an open eye looking downward, crossed out by one thick diagonal line",
 "h_tear": "two hands side by side, each pinching with thumb and index finger, pulling apart a thin vertical jagged crack between them, with one small arrow pointing outward from each hand",
 "h_move": "a walking person with one arrow pointing forward, standing on a floor tile",
 "s_space": "a standing person in the middle of an empty square floor area, four arrows pointing outward from the person to the corners",
 "s_dizzy": "a human head in side profile with one spiral above it",
 "s_warn": "a triangle with one exclamation mark inside it",
}
q = [{"name": k, "model": "banana2", "aspect": "1:1", "size": "1K", "prompt": base + v + "."} for k, v in icons.items()]
q.append({"name": "panel", "model": "banana2", "aspect": "2:3", "size": "2K", "prompt": "Straight-on flat scan of an empty old hospital elevator control panel faceplate, one tall rectangular plate of brushed dark steel with a thin bevelled edge and four small slotted screws in the corners, fills the entire image edge to edge, no background, no perspective. Worn, fine horizontal brushing, soft finger-rub patina in the middle, faint scratches, dry and clean, no rust stains. No buttons, no holes, no letters, no numbers, no text. Evenly lit."})
json.dump(q, open('queue.json', 'w'), ensure_ascii=False, indent=1)
