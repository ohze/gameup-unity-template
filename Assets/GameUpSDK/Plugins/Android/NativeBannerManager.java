package com.gameup.ads;

import android.app.Activity;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.widget.FrameLayout;
import android.widget.TextView;
import android.widget.ImageView;

import com.google.android.gms.ads.AdListener;
import com.google.android.gms.ads.AdLoader;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.nativead.NativeAd;
import com.google.android.gms.ads.nativead.NativeAdView;
import com.google.android.gms.ads.nativead.MediaView;

public class NativeBannerManager {

    public interface AdCallback {
        void onLoaded(); void onFailed(String error); void onDisplayed(); 
        void onClosed(); void onClicked(); void onPaid(double value);
        void onLog(String message);
    }

    public enum AdState { IDLE, LOADING, LOADED, SHOWING }
    
    private static NativeBannerManager instance;
    private View currentAdLayout;
    private NativeAd currentNativeAd;
    private AdState currentState = AdState.IDLE;
    private static int ctaClickRate = 100;
    private AdCallback activeCallback;

    private void sendLog(String msg) {
        if (activeCallback != null) {
            activeCallback.onLog(msg);
        }
    }

    public static void setCtaClickRate(int rate) {
        ctaClickRate = Math.max(0, Math.min(100, rate));
    }

    public static NativeBannerManager getInstance() {
        if (instance == null) instance = new NativeBannerManager();
        return instance;
    }

    public void loadAd(final Activity activity, final String adUnitId, final AdCallback callback) {
        if (currentState == AdState.LOADING) return;

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                currentState = AdState.LOADING;
                activeCallback = callback;
                sendLog("Start Loading Banner ID: " + adUnitId);

                com.google.android.gms.ads.nativead.NativeAdOptions adOptions = 
                    new com.google.android.gms.ads.nativead.NativeAdOptions.Builder()
                        .setAdChoicesPlacement(com.google.android.gms.ads.nativead.NativeAdOptions.ADCHOICES_TOP_LEFT)
                        .build();

                AdLoader adLoader = new AdLoader.Builder(activity, adUnitId)
                        .forNativeAd(new NativeAd.OnNativeAdLoadedListener() {
                            @Override
                            public void onNativeAdLoaded(NativeAd nativeAd) {
                                if (currentState == AdState.IDLE) {
                                    nativeAd.destroy(); return;
                                }
                                if (currentNativeAd != null) currentNativeAd.destroy();
                                currentNativeAd = nativeAd;
                                currentState = AdState.LOADED;
                                sendLog("=> Banner LOADED successfully!");
                                if (callback != null) callback.onLoaded();
                            }
                        })
                        .withAdListener(new AdListener() {
                            @Override
                            public void onAdFailedToLoad(LoadAdError adError) {
                                currentState = AdState.IDLE;
                                sendLog("=> Banner LOAD FAILED: " + adError.getMessage());
                                if (callback != null) callback.onFailed(adError.getMessage());
                            }
                            @Override
                            public void onAdClicked() {
                                sendLog("=> [Google SDK] Valid CTA Click fired! Store/Browser is opening...");
                                if (currentAdLayout != null) {
                                    currentAdLayout.postDelayed(new Runnable() {
                                        @Override
                                        public void run() {
                                            hideAd(activity);
                                            if (callback != null) {
                                                callback.onClicked();
                                                callback.onClosed();
                                            }
                                        }
                                    }, 1500);
                                }
                            }
                        })
                        .withNativeAdOptions(adOptions)
                        .build();

                adLoader.loadAd(new AdRequest.Builder().build());
            }
        });
    }

    public void showAd(final Activity activity, final boolean isTop, final AdCallback callback) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (currentState != AdState.LOADED || currentNativeAd == null) return;
                activeCallback = callback;
                removeCurrentView(activity);

                int layoutId = activity.getResources().getIdentifier("gameup_native_collapsible", "layout", activity.getPackageName());
                currentAdLayout = LayoutInflater.from(activity).inflate(layoutId, null);

                int safeLeft = 0, safeRight = 0, safeTop = 0, safeBottom = 0;
                if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.P) {
                    WindowInsets insets = activity.getWindow().getDecorView().getRootWindowInsets();
                    if (insets != null) {
                        if (insets.getDisplayCutout() != null) {
                            safeLeft = insets.getDisplayCutout().getSafeInsetLeft();
                            safeRight = insets.getDisplayCutout().getSafeInsetRight();
                            safeTop = insets.getDisplayCutout().getSafeInsetTop();
                            safeBottom = insets.getDisplayCutout().getSafeInsetBottom();
                        }
                        safeTop = Math.max(safeTop, insets.getSystemWindowInsetTop());
                        safeBottom = Math.max(safeBottom, insets.getSystemWindowInsetBottom());
                    }
                }
                currentAdLayout.setPadding(safeLeft, isTop ? safeTop : 0, safeRight, isTop ? 0 : safeBottom);

                NativeAdView adView = currentAdLayout.findViewById(activity.getResources().getIdentifier("native_ad_view", "id", activity.getPackageName()));
                MediaView mediaView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_media", "id", activity.getPackageName()));
                TextView headlineView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_headline", "id", activity.getPackageName()));
                android.widget.Button ctaView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_call_to_action", "id", activity.getPackageName()));
                TextView bodyView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_body", "id", activity.getPackageName()));
                ImageView iconView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_app_icon", "id", activity.getPackageName()));
                com.google.android.gms.ads.nativead.AdChoicesView adChoicesView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_choices", "id", activity.getPackageName()));

                adView.setMediaView(mediaView);
                adView.setHeadlineView(headlineView);
                adView.setBodyView(bodyView);
                adView.setIconView(iconView);
                adView.setAdChoicesView(adChoicesView);

                headlineView.setText(currentNativeAd.getHeadline());
                if (currentNativeAd.getCallToAction() != null) { ctaView.setVisibility(View.VISIBLE); ctaView.setText(currentNativeAd.getCallToAction()); } else ctaView.setVisibility(View.INVISIBLE);
                if (currentNativeAd.getBody() != null) { bodyView.setVisibility(View.VISIBLE); bodyView.setText(currentNativeAd.getBody()); } else bodyView.setVisibility(View.GONE);
                if (currentNativeAd.getIcon() != null) { iconView.setVisibility(View.VISIBLE); iconView.setImageDrawable(currentNativeAd.getIcon().getDrawable()); } else iconView.setVisibility(View.GONE);

                // =========================================================================
                // THIẾT KẾ CĂN CHỈNH SÁT MÉP ADCHOICES
                // =========================================================================
                float density = activity.getResources().getDisplayMetrics().density;
                
                // 1. Nhãn Ad (Vàng, chữ đen)
                TextView adBadge = new TextView(activity);
                adBadge.setText("Ad");
                adBadge.setTextColor(android.graphics.Color.BLACK);
                android.graphics.drawable.GradientDrawable adBg = new android.graphics.drawable.GradientDrawable();
                adBg.setColor(android.graphics.Color.parseColor("#FFCC00"));
                adBg.setCornerRadius(3 * density);
                adBadge.setBackground(adBg);
                adBadge.setTextSize(10);
                adBadge.setTypeface(null, android.graphics.Typeface.BOLD);
                adBadge.setGravity(Gravity.CENTER);
                adBadge.setPadding((int)(4*density), 0, (int)(4*density), 0);

                // Chiều cao fix chuẩn 15dp bằng với icon AdChoices, Y = 0, X = 18dp
                FrameLayout.LayoutParams badgeParams = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WRAP_CONTENT, (int)(15 * density));
                badgeParams.gravity = Gravity.TOP | Gravity.LEFT;
                badgeParams.setMargins((int)(18 * density), 0, 0, 0); 
                adView.addView(adBadge, badgeParams);

                // 2. Chữ Sponsored (Ở GIỮA)
                TextView sponsoredText = new TextView(activity);
                String sponStr = "Sponsored";
                if (currentNativeAd.getAdvertiser() != null || currentNativeAd.getStore() != null) {
                    String advName = currentNativeAd.getAdvertiser() != null ? currentNativeAd.getAdvertiser() : currentNativeAd.getStore();
                    sponStr += " • " + advName;
                    if (currentNativeAd.getAdvertiser() != null) adView.setAdvertiserView(sponsoredText);
                    else adView.setStoreView(sponsoredText);
                }
                sponsoredText.setText(sponStr);
                sponsoredText.setTextColor(android.graphics.Color.WHITE);
                sponsoredText.setTextSize(11);
                sponsoredText.setTypeface(null, android.graphics.Typeface.BOLD);
                sponsoredText.setShadowLayer(5, 1, 1, android.graphics.Color.parseColor("#FF000000"));

                // Kéo lên sát đỉnh màn hình (Y = 0)
                FrameLayout.LayoutParams sponParams = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT);
                sponParams.gravity = Gravity.TOP | Gravity.CENTER_HORIZONTAL;
                sponParams.setMargins(0, 0, 0, 0);
                adView.addView(sponsoredText, sponParams);

                if (adChoicesView != null) {
                    adChoicesView.bringToFront();
                }

                ImageView blurBg = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_blur_bg", "id", activity.getPackageName()));
                if (blurBg != null && currentNativeAd.getImages() != null && currentNativeAd.getImages().size() > 0) {
                    try {
                        android.graphics.drawable.Drawable drawable = currentNativeAd.getImages().get(0).getDrawable();
                        if (drawable instanceof android.graphics.drawable.BitmapDrawable) {
                            android.graphics.Bitmap bitmap = ((android.graphics.drawable.BitmapDrawable) drawable).getBitmap();
                            int w = Math.round(bitmap.getWidth() * 0.1f);
                            int h = Math.round(bitmap.getHeight() * 0.1f);
                            if (w > 0 && h > 0) {
                                android.graphics.Bitmap scaled = android.graphics.Bitmap.createScaledBitmap(bitmap, w, h, true);
                                blurBg.setImageBitmap(scaled);
                                blurBg.setColorFilter(android.graphics.Color.argb(180, 255, 255, 255)); 
                            }
                        }
                    } catch (Exception ignored) { }
                }

                int roll = new java.util.Random().nextInt(100);
                boolean enableTrap = (roll < ctaClickRate);
                
                View btnClose = currentAdLayout.findViewById(activity.getResources().getIdentifier("btn_close_ad", "id", activity.getPackageName()));

                if (enableTrap) {
                    View overlayTrap = new View(activity);
                    overlayTrap.setBackgroundColor(android.graphics.Color.TRANSPARENT);
                    ((ViewGroup) adView).addView(overlayTrap, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
                    overlayTrap.bringToFront();
                    
                    adView.setCallToActionView(overlayTrap);

                    if (btnClose != null) { btnClose.setOnClickListener(null); btnClose.setClickable(false); }
                    if (blurBg != null) { blurBg.setOnClickListener(null); blurBg.setClickable(false); }
                    currentAdLayout.setOnClickListener(null);
                    adView.setOnClickListener(null);
                } else {
                    adView.setCallToActionView(ctaView);

                    View.OnClickListener normalCloseTrigger = new View.OnClickListener() {
                        @Override
                        public void onClick(View v) {
                            hideAd(activity);
                            if (callback != null) callback.onClosed();
                        }
                    };

                    if (btnClose != null) { btnClose.setOnClickListener(normalCloseTrigger); btnClose.bringToFront(); }
                    if (blurBg != null) blurBg.setOnClickListener(normalCloseTrigger);
                    currentAdLayout.setOnClickListener(normalCloseTrigger);
                }

                adView.setNativeAd(currentNativeAd);

                FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.WRAP_CONTENT);
                params.gravity = isTop ? Gravity.TOP : Gravity.BOTTOM;

                ViewGroup rootView = activity.findViewById(android.R.id.content);
                rootView.addView(currentAdLayout, params);

                currentState = AdState.SHOWING;
                if (callback != null) callback.onDisplayed();
            }
        });
    }

    public void hideAd(final Activity activity) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                removeCurrentView(activity);
                if (currentNativeAd != null) { 
                    currentNativeAd.destroy(); 
                    currentNativeAd = null; 
                }
                currentState = AdState.IDLE;
            }
        });
    }

    private void removeCurrentView(Activity activity) {
        if (currentAdLayout != null) {
            ViewGroup rootView = activity.findViewById(android.R.id.content);
            rootView.removeView(currentAdLayout);
            currentAdLayout = null;
        }
    }
}