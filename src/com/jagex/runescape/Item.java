package com.jagex.runescape;

import com.jagex.runescape.definition.ItemDefinition;

final class Item extends Animable {

	public int itemId;

	public int x;

	public int y;
	public int itemCount;

	public Item() {
	}

	@Override
	public final Model getRotatedModel() {
		ItemDefinition itemDef = ItemDefinition.getDefinition(itemId);
		return itemDef.getAmountModel(itemCount);
	}
}
