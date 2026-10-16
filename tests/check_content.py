"""检查递进配方、容量、翻译和发布包内的素材引用。"""

import csv
import json
import struct
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TIERS = [
    ("large_warehouse", "warehouse", 2, 30),
    ("larger_warehouse", "slw_large_warehouse", 3, 100),
    ("huge_warehouse", "slw_larger_warehouse", 5, 800),
]


class ContentTests(unittest.TestCase):
    def test_upgrade_chain(self):
        with (ROOT / "localization.tsv").open(encoding="utf-8-sig", newline="") as source:
            translations = {row["Term"]: row for row in csv.DictReader(source, delimiter="\t")}
        for name, ingredient, count, capacity in TIERS:
            with self.subTest(tier=name):
                card = json.loads((ROOT / "Cards" / (name + ".json")).read_text(encoding="utf-8"))
                blueprint = json.loads((ROOT / "Blueprints" / (name + ".json")).read_text(encoding="utf-8"))
                recipe = blueprint["subprints"][0]
                self.assertEqual(card["id"], "slw_" + name)
                self.assertEqual(card["_Capacity"], capacity)
                self.assertEqual(card.get("_IsFoil", False), name == "huge_warehouse")
                self.assertEqual(card["script"], "SLWarehouse.UpgradedWarehouse")
                self.assertEqual(recipe["ResultCard"], card["id"])
                for field in ("RequiredCards", "CardsToRemove"):
                    self.assertEqual([part.strip() for part in recipe[field].split(",")], [ingredient] * count)
                self.assertTrue(blueprint["needsExactMatch"])
                self.assertGreater(recipe["Time"], 0)
                for term in (card["nameTerm"], card["descriptionTerm"], recipe["StatusTerm"]):
                    for language in ("English", "Chinese (Simplified)"):
                        self.assertTrue(translations[term][language])

    def test_approved_icons_and_release_contents(self):
        with zipfile.ZipFile(ROOT / "dist" / "sl_warehouse.zip") as package:
            paths = {name.replace("\\", "/"): name for name in package.namelist()}
            self.assertIn("sl_warehouse/sl_warehouse.dll", paths)
            self.assertNotIn("sl_warehouse/config.json", paths)
            self.assertFalse(any("smoke" in name.lower() or "candidate" in name.lower() for name in paths))
            for name, *_ in TIERS:
                with self.subTest(icon=name):
                    image = (ROOT / "Icons" / (name + ".png")).read_bytes()
                    self.assertEqual(image[:8], b"\x89PNG\r\n\x1a\n")
                    width, height, depth, color = struct.unpack(">IIBB", image[16:26])
                    self.assertEqual(width, height)
                    self.assertGreaterEqual(width, 256)
                    self.assertEqual((depth, color), (8, 6))
                    key = "sl_warehouse/Icons/" + name + ".png"
                    self.assertEqual(package.read(paths[key]), image)
                    for folder in ("Cards", "Blueprints"):
                        key = "sl_warehouse/" + folder + "/" + name + ".json"
                        self.assertEqual(json.loads(package.read(paths[key]))["icon"], name + ".png")


if __name__ == "__main__":
    unittest.main(verbosity=2)
