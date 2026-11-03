const { test, expect } = require('@playwright/test');
test('user books a room, sees a conflict, cancels, and rebooks',async ({page})=>{
    await page.goto('/');
    await expect(page.locator('.room-card')).toHaveCount(3);
    await page.locator('#day').fill('2026-11-04');
    await page.locator('#name').fill('UI Test User');
    await page.locator('#start').fill('10:00');
    await page.locator('#end').fill('11:00');
    await page.getByRole('button',{name:'Book room',exact:true}).click();
    await expect(page.getByRole('status')).toHaveText('Room booked successfully.');
    await expect(page.locator('.booking')).toHaveCount(1);
    await page.getByRole('button',{name:'Book room',exact:true}).click();
    await expect(page.getByRole('status')).toContainText('already booked');
    await page.getByRole('button',{name:'Cancel booking for UI Test User'}).click();
    await expect(page.locator('.booking')).toHaveCount(0);
    await page.getByRole('button',{name:'Book room',exact:true}).click();
    await expect(page.getByRole('status')).toHaveText('Room booked successfully.');
    await expect(page.locator('.booking')).toHaveCount(1);
});
